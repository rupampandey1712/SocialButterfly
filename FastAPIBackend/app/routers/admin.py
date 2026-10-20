"""/api/Admin and the admin-only WeatherForecast sample. The middleware already rejects non-admins;
each route re-checks the role so it stays safe if mounted elsewhere."""

from __future__ import annotations

import secrets
from datetime import datetime, timedelta

from fastapi import APIRouter, Depends, HTTPException, Response
from sqlalchemy.orm import Session, joinedload

from app.deps import current_user, get_db
from app.models import Post, User

router = APIRouter()


def require_admin(user: User = Depends(current_user)) -> User:
    if user.role != "Admin": raise HTTPException(403)
    return user


@router.get("/health")
def health():
    return {"status": "ok"}


@router.get("/WeatherForecast")
def weather_forecast(user: User = Depends(require_admin)):
    summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"]
    forecast = []
    for day in range(1, 6):
        temperature = secrets.randbelow(76) - 20
        forecast.append({"date": (datetime.now() + timedelta(days=day)).isoformat(), "temperatureC": temperature,
                         "temperatureF": 32 + int(temperature / 0.5556), "summary": secrets.choice(summaries)})
    return forecast


@router.get("/api/Admin/GetUserDetail")
def admin_users(user: User = Depends(require_admin), db: Session = Depends(get_db)):
    return [{"id": u.id, "name": u.name, "email": u.email, "role": u.role, "imagepath": u.image_path, "isDeleted": u.is_deleted}
            for u in db.query(User).filter(User.role != "Admin")]


@router.get("/api/Admin/PostByDate")
def admin_posts_by_date(user: User = Depends(require_admin), db: Session = Depends(get_db)):
    posts = db.query(Post).options(joinedload(Post.user)).filter(Post.created_at.is_not(None))
    return [{"createdAt": p.created_at.strftime("%Y-%m-%d"), "name": (p.user.name or "").upper() if p.user else "",
             "image": p.user.image_path if p.user else None} for p in posts]


@router.put("/api/Admin")
def admin_toggle_delete(id: int, IsDeleted: bool, user: User = Depends(require_admin), db: Session = Depends(get_db)):
    target = db.get(User, id)
    if not target: raise HTTPException(400, "User not found")
    target.is_deleted = IsDeleted; db.commit()
    return Response(status_code=200)
