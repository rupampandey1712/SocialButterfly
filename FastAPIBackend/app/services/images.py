"""ComfyUI image generation jobs. Blocking work (database, blob upload, file reads) runs on worker
threads so a long job never stalls the event loop that serves requests."""

from __future__ import annotations

import asyncio
import json
import re
from datetime import timedelta
from typing import TYPE_CHECKING, Optional

import httpx
from sqlalchemy import and_, or_, update

from app.db import utcnow
from app.models import GeneratedImage

if TYPE_CHECKING:
    from app.context import AppContext

IMAGE_STYLES = {
    "none": "", "photo": ", professional photograph, natural light, 35mm, highly detailed",
    "cinematic": ", cinematic film still, dramatic lighting, shallow depth of field, color graded",
    "anime": ", anime illustration, vibrant colors, clean line art, studio quality",
    "watercolor": ", delicate watercolor painting, soft washes, textured paper",
    "3d": ", 3D render, soft studio lighting, octane render, highly detailed",
    "neon": ", neon cyberpunk aesthetic, glowing lights, night, rain reflections",
    "sketch": ", pencil sketch, detailed graphite shading, on paper",
}
SIZES = {"square": (1024, 1024), "portrait": (768, 1152), "landscape": (1152, 768)}
PROHIBITED = re.compile(
    r"\b(child|children|kid|kids|minor|minors|underage|teen|preteen|loli|toddler)\b[\s\S]*\b(nude|naked|sexual|sexy|nsfw|explicit|porn)\b"
    r"|\b(nude|naked|sexual|sexy|nsfw|explicit|porn)\b[\s\S]*\b(child|children|kid|kids|minor|minors|underage|teen|preteen|loli|toddler)\b", re.I)
GENERATION_TIMEOUT = 600
# A Running job older than this was abandoned by a crashed worker and may be claimed again.
STALE_AFTER = timedelta(seconds=GENERATION_TIMEOUT + 300)


def claim_job(ctx: "AppContext", job_id: int) -> Optional[tuple[str, int, int, int]]:
    """Atomically move a job to Running. Returns (prompt, width, height, seed), or None when the job is
    finished or another worker is already running it (e.g. a redelivered Service Bus message)."""
    now = utcnow()
    with ctx.SessionLocal() as session:
        claimable = or_(GeneratedImage.status == "Queued",
                        and_(GeneratedImage.status == "Running",
                             or_(GeneratedImage.started_at.is_(None), GeneratedImage.started_at < now - STALE_AFTER)))
        claimed = session.execute(update(GeneratedImage).where(GeneratedImage.id == job_id, claimable)
                                  .values(status="Running", started_at=now)).rowcount
        session.commit()
        if not claimed:
            return None
        job = session.get(GeneratedImage, job_id)
        return job.prompt + IMAGE_STYLES.get(job.style, ""), job.width, job.height, job.seed


def finish_job(ctx: "AppContext", job_id: int, image_path: Optional[str] = None, error: Optional[str] = None) -> None:
    with ctx.SessionLocal() as session:
        job = session.get(GeneratedImage, job_id)
        if job:
            job.status = "Failed" if error else "Succeeded"
            job.image_path = image_path or job.image_path
            job.error = error
            job.completed_at = utcnow()
            session.commit()


def build_workflow(ctx: "AppContext", prompt: str, width: int, height: int, seed: int) -> dict:
    settings = ctx.settings
    workflow = json.loads(settings.comfy_workflow_path.read_text(encoding="utf-8"))
    workflow[settings.comfy_prompt_node]["inputs"]["text"] = prompt
    workflow[settings.comfy_latent_node]["inputs"].update({"width": width, "height": height})
    workflow[settings.comfy_sampler_node]["inputs"]["seed"] = seed
    return workflow


async def process_image(ctx: "AppContext", job_id: int) -> None:
    claim = await asyncio.to_thread(claim_job, ctx, job_id)
    if not claim:
        return
    try:
        workflow = await asyncio.to_thread(build_workflow, ctx, *claim)
        base = ctx.settings.comfyui_url
        async with httpx.AsyncClient(timeout=60) as client:
            queued = await client.post(base + "/prompt", json={"prompt": workflow, "client_id": "socialmedia-fastapi"})
            queued.raise_for_status()
            prompt_id = queued.json()["prompt_id"]
            loop = asyncio.get_running_loop(); deadline = loop.time() + GENERATION_TIMEOUT; image_info = None
            while loop.time() < deadline and not image_info:
                await asyncio.sleep(2)
                response = await client.get(f"{base}/history/{prompt_id}"); response.raise_for_status()
                history = response.json().get(prompt_id)
                if history:
                    if history.get("status", {}).get("status_str") == "error":
                        raise RuntimeError("The image generator failed to create this image.")
                    image_info = next((output["images"][0] for output in history.get("outputs", {}).values() if output.get("images")), None)
            if not image_info:
                raise TimeoutError("The image took too long to generate.")
            image = await client.get(base + "/view", params={k: image_info.get(k, "") for k in ("filename", "subfolder", "type")})
            image.raise_for_status()
        image_path = await asyncio.to_thread(ctx.storage.save_bytes, image.content, "generated.png", "ai-images")
        await asyncio.to_thread(finish_job, ctx, job_id, image_path)
    except Exception as exc:
        await asyncio.to_thread(finish_job, ctx, job_id, None, str(exc)[:300] or "Image generation failed.")
