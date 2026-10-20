"""SocialMediaWeb FastAPI backend: application factory.

`uvicorn app.main:app` serves the instance built from environment variables at import time.
"""

from __future__ import annotations

from contextlib import asynccontextmanager
from typing import Optional

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles
from sqlalchemy.orm import sessionmaker

from app.config import Settings
from app.context import AppContext
from app.db import make_engine
from app.migrate import upgrade_database
from app.models import (ChatMessage, Comment, Follow, Friend, GeneratedImage, Like, Notification, Post,  # noqa: F401
                        Story, StoryView, User, VerificationCode)
from app.routers import account, admin, chat, follows, friends, images, notifications, posts, stories
from app.security.middleware import install_security_middleware
from app.security.passwords import hash_password
from app.security.tokens import TokenService
from app.services.blob_storage import BlobStorage
from app.services.workers import start_workers

MEDIA_FOLDERS = ("Images", "posts", "stories", "music", "ai-images")


def seed_admin(ctx: AppContext) -> None:
    """Promote ADMIN_EMAIL's account, or create it when ADMIN_PASSWORD is set (8+ characters)."""
    email, password = ctx.settings.admin_email, ctx.settings.admin_password
    if not email: return
    with ctx.SessionLocal() as db:
        admin = db.query(User).filter(User.email == email).first()
        if admin:
            admin.role = "Admin"
        elif len(password) >= 8:
            username = "admin"; suffix = 1
            while db.query(User).filter_by(username=username).first(): username = f"admin{suffix}"; suffix += 1
            db.add(User(name="Administrator", username=username, email=email, email_confirmed=True, role="Admin",
                        password_hash=hash_password(password)))
        db.commit()


@asynccontextmanager
async def lifespan(app: FastAPI):
    tasks = start_workers(app.state.ctx)
    try:
        yield
    finally:
        for task in tasks: task.cancel()
        for task in tasks:
            try: await task
            except BaseException: pass


async def validation_error(request: Request, exc: RequestValidationError) -> JSONResponse:
    """Malformed requests get a 400 problem-details body, like ASP.NET Core's [ApiController] model validation."""
    errors: dict[str, list[str]] = {}
    for error in exc.errors():
        field = ".".join(str(part) for part in error.get("loc", ())[1:]) or "body"
        errors.setdefault(field, []).append(error.get("msg", "Invalid value."))
    return JSONResponse({"type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                         "title": "One or more validation errors occurred.", "status": 400, "errors": errors}, status_code=400)


def create_app(settings: Optional[Settings] = None) -> FastAPI:
    settings = settings or Settings.from_env()
    engine = make_engine(settings.database_url)
    upgrade_database(engine)
    settings.media_root.mkdir(parents=True, exist_ok=True)
    ctx = AppContext(settings=settings, engine=engine, SessionLocal=sessionmaker(bind=engine, expire_on_commit=False),
                     storage=BlobStorage(settings.media_root),
                     tokens=TokenService(settings.jwt_secret, settings.jwt_issuer, settings.jwt_audience, settings.token_minutes))
    seed_admin(ctx)

    app = FastAPI(title="SocialMediaWeb FastAPI", version="1.1.0", lifespan=lifespan)
    app.state.ctx = ctx
    # Added before the security gate, so the gate is the outer layer and sees every request first.
    app.add_middleware(CORSMiddleware, allow_origins=list(settings.cors_origins), allow_credentials=True, allow_methods=["*"], allow_headers=["*"])
    install_security_middleware(app)
    app.add_exception_handler(RequestValidationError, validation_error)
    for folder in MEDIA_FOLDERS:
        app.mount(f"/{folder}", StaticFiles(directory=settings.media_root / folder, check_dir=False), name=folder)
    for module in (admin, account, posts, friends, follows, chat, notifications, stories, images):
        app.include_router(module.router)
    return app


app = create_app()
# Handy for scripts and tests: the session factory and engine behind `app`.
SessionLocal = app.state.ctx.SessionLocal
engine = app.state.ctx.engine
