"""FastAPI dependencies: application context, database session and the signed-in member."""

from __future__ import annotations

from typing import Iterator, Optional

from fastapi import Depends, HTTPException, Request
from fastapi.responses import JSONResponse
from sqlalchemy.orm import Session

from app.context import AppContext
from app.models import User

SESSION_COOKIE = "sb_session"


def get_ctx(request: Request) -> AppContext:
    return request.app.state.ctx


def get_db(ctx: AppContext = Depends(get_ctx)) -> Iterator[Session]:
    session = ctx.SessionLocal()
    try:
        yield session
    finally:
        session.close()


def request_ip(request: Request) -> str:
    return request.client.host if request.client else "unknown"


def authenticate(ctx: AppContext, request: Request, db: Session) -> User:
    """The active member named by the bearer token or session cookie; 401 otherwise."""
    auth = request.headers.get("authorization", "")
    token = auth[7:] if auth.lower().startswith("bearer ") else request.cookies.get(SESSION_COOKIE)
    user_id = ctx.tokens.user_id(token)
    user = db.get(User, user_id) if user_id is not None else None
    if not user or user.is_deleted:
        raise HTTPException(401, "Authentication required")
    return user


def current_user(request: Request, ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)) -> User:
    return authenticate(ctx, request, db)


def set_session_cookie(response, ctx: AppContext, user: User) -> None:
    response.set_cookie(SESSION_COOKIE, ctx.tokens.issue(user.id, user.role), httponly=True, samesite="strict",
                        secure=ctx.settings.cookie_secure, max_age=ctx.settings.token_minutes * 60)


def bot_challenge(ctx: AppContext, request: Request, scope: str, account_key: Optional[str]) -> Optional[JSONResponse]:
    """A 428 proof-of-work challenge when this IP/account looks automated and sent no valid proof."""
    difficulty = ctx.bot_defense.required_difficulty(request_ip(request), account_key)
    if not difficulty or ctx.pow.verify(request.headers.get("X-Bot-Proof"), difficulty, scope):
        return None
    return JSONResponse({"error": "challenge_required", "message": "Verifying you're human…",
                         "challenge": ctx.pow.issue(difficulty, scope), "difficulty": difficulty}, status_code=428)
