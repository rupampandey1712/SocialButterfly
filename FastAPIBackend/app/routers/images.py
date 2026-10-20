"""/api/Images (ComfyUI studio) and /api/GenAi (NVIDIA NIM text)."""

from __future__ import annotations

import asyncio
import re
import secrets
from datetime import timedelta
from typing import Optional

import httpx
from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException
from sqlalchemy.orm import Session

from app.context import AppContext
from app.db import utcnow
from app.deps import current_user, get_ctx, get_db
from app.models import GeneratedImage, Post, User
from app.schemas import GenAiRequest, ImageRequest, ShareImageRequest
from app.services import service_bus
from app.services.images import IMAGE_STYLES, PROHIBITED, SIZES
from app.services.notifications import emit_notification

router = APIRouter()

DAILY_LIMIT = 25
ACTIVE_LIMIT = 2


def image_dto(j: GeneratedImage, ctx: AppContext) -> dict:
    return {"id": j.id, "prompt": j.prompt, "style": j.style, "width": j.width, "height": j.height, "seed": j.seed, "status": j.status,
            "imagePath": ctx.storage.public_url(j.image_path), "error": j.error, "postId": j.post_id, "createdAt": j.created_at,
            "startedAt": j.started_at, "completedAt": j.completed_at}


def usage(db: Session, user_id: int) -> tuple[int, int]:
    """(jobs queued or running, jobs started in the last 24 hours)."""
    mine = db.query(GeneratedImage).filter(GeneratedImage.user_id == user_id)
    return (mine.filter(GeneratedImage.status.in_(("Queued", "Running"))).count(),
            mine.filter(GeneratedImage.created_at > utcnow() - timedelta(days=1)).count())


@router.get("/api/Images/status")
async def image_status(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    active, used = await asyncio.to_thread(usage, db, user.id)
    online = False
    try:
        async with httpx.AsyncClient(timeout=3) as client:
            online = (await client.get(ctx.settings.comfyui_url + "/system_stats")).is_success
    except Exception:
        pass
    return {"online": online, "activeJobs": active, "remainingToday": max(DAILY_LIMIT - used, 0), "styles": list(IMAGE_STYLES)}


@router.post("/api/Images")
def generate_image(body: ImageRequest, background_tasks: BackgroundTasks, user: User = Depends(current_user),
                   ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    prompt = re.sub(r"\s+", " ", "".join(c for c in body.prompt if ord(c) >= 32 or c == "\n").strip())[:800]
    if len(prompt) < 3: raise HTTPException(400, "Describe the image you want (at least a few words).")
    if PROHIBITED.search(prompt): raise HTTPException(400, "This prompt isn't allowed.")
    style = (body.style or "none").lower(); aspect = (body.aspect or "square").lower()
    if style not in IMAGE_STYLES: raise HTTPException(400, "Unknown style.")
    if aspect not in SIZES: raise HTTPException(400, "Aspect must be square, portrait or landscape.")
    active, used = usage(db, user.id)
    if active >= ACTIVE_LIMIT: raise HTTPException(400, "You already have images being created. Wait for them to finish.")
    if used >= DAILY_LIMIT: raise HTTPException(400, f"You've reached today's limit of {DAILY_LIMIT} images. Try again tomorrow.")
    width, height = SIZES[aspect]
    seed = body.seed if body.seed and 0 < body.seed < 2**63 else secrets.randbelow(2**31 - 1) + 1
    job = GeneratedImage(user_id=user.id, prompt=prompt, style=style, width=width, height=height, seed=seed, status="Queued")
    db.add(job); db.commit(); db.refresh(job)
    if service_bus.enabled():
        try:
            service_bus.publish("image-generation", {"JobId": job.id}, f"image-{job.id}", "ImageGenerationMessage")
        except Exception:
            job.status = "Failed"; job.error = "The image service is busy right now. Please try again."; job.completed_at = utcnow(); db.commit()
    else:
        background_tasks.add_task(ctx.process_image, job.id)
    return image_dto(job, ctx)


@router.get("/api/Images/mine")
def my_images(take: int = 24, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    jobs = db.query(GeneratedImage).filter_by(user_id=user.id).order_by(GeneratedImage.created_at.desc()).limit(min(max(take, 1), 60))
    return [image_dto(j, ctx) for j in jobs]


def own_job(db: Session, user: User, job_id: int) -> GeneratedImage:
    job = db.query(GeneratedImage).filter_by(id=job_id, user_id=user.id).first()
    if not job: raise HTTPException(404)
    return job


@router.get("/api/Images/{job_id:int}")
def get_image(job_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return image_dto(own_job(db, user, job_id), ctx)


@router.post("/api/Images/{job_id:int}/share")
def share_image(job_id: int, body: Optional[ShareImageRequest] = None, user: User = Depends(current_user), db: Session = Depends(get_db)):
    job = own_job(db, user, job_id)
    if job.status != "Succeeded" or not job.image_path: raise HTTPException(400, "This image isn't ready yet.")
    if job.post_id and db.get(Post, job.post_id): return {"postId": job.post_id}
    caption = (body.caption if body and body.caption else job.prompt)[:2200]
    p = Post(user_id=user.id, image_path=job.image_path, description=caption); db.add(p); db.commit(); db.refresh(p)
    job.post_id = p.id; emit_notification(db, "PostCreated", user.id, post_id=p.id); db.commit()
    return {"postId": p.id}


@router.post("/api/GenAi/generate-text")
def genai_text(body: GenAiRequest, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx)):
    if not body.prompt.strip(): raise HTTPException(400, "Prompt is required.")
    settings = ctx.settings
    if not settings.nim_base_url or not settings.nim_api_key or not settings.nim_model:
        raise HTTPException(503, "Python GenAI service unavailable: configure NVIDIA NIM settings.")
    try:
        from openai import OpenAI
        client = OpenAI(base_url=settings.nim_base_url, api_key=settings.nim_api_key)
        result = client.chat.completions.create(model=settings.nim_model, messages=[{"role": "user", "content": body.prompt}], max_tokens=250)
        return {"text": result.choices[0].message.content or ""}
    except Exception as exc:
        raise HTTPException(503, f"Python GenAI service unavailable: {str(exc)[:250]}")
