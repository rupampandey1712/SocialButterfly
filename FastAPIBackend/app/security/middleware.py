"""Request gate: body size, origin + CSRF checks, authentication, admin paths, rate limits and security headers."""

from __future__ import annotations

import asyncio
import hmac
import secrets

from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.responses import JSONResponse

from app.deps import authenticate, request_ip

MAX_BODY = 80 * 1024 * 1024
SAFE_METHODS = {"GET", "HEAD", "OPTIONS", "TRACE"}
ANONYMOUS = {
    ("GET", "/api/Account/session"), ("POST", "/api/Account/logout"),
    ("POST", "/api/Account/register"), ("POST", "/api/Account/login"),
    ("GET", "/api/Account/availability"), ("POST", "/api/Account/code/send"),
    ("POST", "/api/Account/code/confirm"),
}
# Requests per minute, mirroring the .NET rate-limiter policies.
AUTH_LIMIT = 10
CODES_LIMIT = 8
AI_LIMIT = 6


def rate_limit_rule(method: str, path: str, ip: str, actor) -> tuple[str, int] | None:
    if method == "POST" and path in {"/api/Account/login", "/api/Account/register"}:
        return f"{ip}:auth", AUTH_LIMIT
    if method == "PUT" and path == "/api/Account/me/password":
        # Same budget as sign-in, per member: guessing the current password is a login attempt.
        return f"{actor.id}:password", AUTH_LIMIT
    if method == "POST" and path.startswith("/api/Account/code/"):
        return f"{ip}:codes", CODES_LIMIT
    if method == "POST" and path == "/api/Images":
        return f"{actor.id if actor else ip}:images", AI_LIMIT
    return None


def load_actor(ctx, request: Request):
    with ctx.SessionLocal() as db:
        return authenticate(ctx, request, db)


def install_security_middleware(app: FastAPI) -> None:
    def xsrf_cookie(request: Request, response: Response) -> Response:
        if not request.cookies.get("XSRF-TOKEN"):
            response.set_cookie("XSRF-TOKEN", secrets.token_hex(32), httponly=False, samesite="strict",
                                secure=app.state.ctx.settings.cookie_secure, path="/")
        return response

    @app.middleware("http")
    async def security_gate(request: Request, call_next):
        ctx = app.state.ctx
        settings = ctx.settings
        try:
            if int(request.headers.get("content-length", "0")) > MAX_BODY:
                return Response("Request body is too large.", status_code=413)
        except ValueError:
            return Response("Invalid content length.", status_code=400)
        path = request.url.path
        if path.startswith("/api") and request.method not in SAFE_METHODS:
            origin = request.headers.get("origin") or "/".join(request.headers.get("referer", "").split("/", 3)[:3])
            allowed = set(settings.cors_origins) | {f"{request.url.scheme}://{request.headers.get('host', '')}"}
            if origin and origin not in allowed:
                return Response("Cross-site request blocked.", status_code=403)
            if not request.headers.get("authorization", "").lower().startswith("bearer "):
                cookie = request.cookies.get("XSRF-TOKEN", ""); header = request.headers.get("X-XSRF-TOKEN", "")
                if not cookie or not header or not hmac.compare_digest(cookie, header):
                    return Response("Missing or invalid CSRF token. Reload the page and try again.", status_code=403)
        actor = None
        needs_user = request.method != "OPTIONS" and (
            (path.startswith("/api/") and (request.method, path) not in ANONYMOUS) or path == "/WeatherForecast")
        if needs_user:
            try:
                actor = await asyncio.to_thread(load_actor, ctx, request)
            except HTTPException as exc:
                return xsrf_cookie(request, JSONResponse({"detail": exc.detail}, status_code=exc.status_code))
            admin_only = path == "/WeatherForecast" or path == "/api/Admin" or path.startswith("/api/Admin/")
            if admin_only and actor.role != "Admin":
                return xsrf_cookie(request, JSONResponse({"detail": "Forbidden"}, status_code=403))
        rule = rate_limit_rule(request.method, path, request_ip(request), actor)
        if rule and not ctx.rate_limiter.allow(*rule):
            return Response("Too many attempts. Please wait a minute and try again.", status_code=429)
        response = await call_next(request)
        if path.startswith("/api"):
            xsrf_cookie(request, response)
        response.headers["X-Content-Type-Options"] = "nosniff"; response.headers["X-Frame-Options"] = "DENY"
        response.headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'"
        response.headers["Cross-Origin-Opener-Policy"] = "same-origin"
        response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
        response.headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()"
        if settings.cookie_secure:
            response.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"
        return response
