"""Environment-driven settings, read once when an application instance is created."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEV_SECRET = "dev-only-change-this-secret-before-deployment"


def _flag(name: str, default: bool) -> bool:
    value = os.getenv(name)
    return default if value is None else value.strip().lower() in {"1", "true", "yes", "on"}


@dataclass(frozen=True)
class Settings:
    app_env: str
    database_url: str
    media_root: Path
    jwt_secret: str
    jwt_issuer: str
    jwt_audience: str
    token_minutes: int
    pow_secret: str
    cors_origins: tuple[str, ...]
    cookie_secure: bool
    expose_dev_codes: bool
    admin_email: str
    admin_password: str
    comfyui_url: str
    comfy_workflow_path: Path
    comfy_prompt_node: str
    comfy_latent_node: str
    comfy_sampler_node: str
    worker_mode: str
    nim_base_url: str
    nim_api_key: str
    nim_model: str

    @property
    def is_development(self) -> bool:
        return self.app_env.lower() in {"development", "dev"}

    @classmethod
    def from_env(cls) -> "Settings":
        app_env = os.getenv("APP_ENV", "Development")
        development = app_env.lower() in {"development", "dev"}
        secret = os.getenv("JWT_SECRET", DEV_SECRET)
        if not development and (len(secret) < 32 or secret == DEV_SECRET):
            raise RuntimeError("Set JWT_SECRET to a private random value of at least 32 characters outside development.")
        cookie_secure = os.getenv("COOKIE_SECURE")
        return cls(
            app_env=app_env,
            database_url=os.getenv("DATABASE_URL", f"sqlite:///{ROOT / 'socialmedia.db'}"),
            media_root=Path(os.getenv("MEDIA_ROOT", str(ROOT / "media"))),
            jwt_secret=secret,
            jwt_issuer=os.getenv("JWT_VALID_ISSUER", "http://localhost:8080"),
            jwt_audience=os.getenv("JWT_VALID_AUDIENCE", "http://localhost:8080"),
            token_minutes=int(os.getenv("JWT_TOKEN_VALIDITY_IN_MINUTES", "60")),
            pow_secret=os.getenv("POW_SECRET") or secret,
            cors_origins=tuple(x.strip() for x in os.getenv("CORS_ORIGINS", "http://localhost:3000,http://localhost:5173").split(",") if x.strip()),
            cookie_secure=cookie_secure == "1" if cookie_secure is not None else not development,
            expose_dev_codes=development and _flag("VERIFICATION_EXPOSE_DEV_CODES", True),
            admin_email=os.getenv("ADMIN_EMAIL", "").strip().lower(),
            admin_password=os.getenv("ADMIN_PASSWORD", ""),
            comfyui_url=os.getenv("COMFYUI_URL", "http://127.0.0.1:8188").rstrip("/"),
            comfy_workflow_path=Path(os.getenv("COMFY_WORKFLOW_PATH") or ROOT / "ComfyUI" / "flux-schnell-txt2img.json"),
            comfy_prompt_node=os.getenv("COMFY_PROMPT_NODE", "6"),
            comfy_latent_node=os.getenv("COMFY_LATENT_NODE", "5"),
            comfy_sampler_node=os.getenv("COMFY_SAMPLER_NODE", "3"),
            worker_mode=os.getenv("SERVICEBUS_WORKER_MODE", "python").lower(),
            nim_base_url=os.getenv("NIM_BASE_URL", ""),
            nim_api_key=os.getenv("NVIDIA_API_KEY", ""),
            nim_model=os.getenv("NIM_MODEL", ""),
        )
