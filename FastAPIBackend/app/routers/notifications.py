"""/api/Notifications."""

from __future__ import annotations

from fastapi import APIRouter, Depends, Response
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.context import AppContext
from app.deps import current_user, get_ctx, get_db
from app.models import Notification, Post, User
from app.views import author, users_by_id

router = APIRouter(prefix="/api/Notifications")


@router.get("/me")
def notifications(take: int = 30, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(Notification).filter_by(recipient_id=user.id)
            .order_by(Notification.created_at.desc(), Notification.id.desc()).limit(min(max(take, 1), 100)).all())
    actors = users_by_id(db, {n.actor_id for n in rows})
    post_ids = {n.post_id for n in rows if n.post_id}
    images = dict(db.execute(select(Post.id, Post.image_path).where(Post.id.in_(post_ids))).all()) if post_ids else {}
    return [{"id": n.id, "type": n.type, "actor": author(actors[n.actor_id], ctx.storage), "postId": n.post_id,
             "postImagePath": ctx.storage.public_url(images.get(n.post_id)), "text": n.text, "createdAt": n.created_at, "isRead": n.is_read}
            for n in rows if n.actor_id in actors and not actors[n.actor_id].is_deleted]


@router.get("/me/unread-count")
def unread(user: User = Depends(current_user), db: Session = Depends(get_db)):
    return {"count": db.query(Notification).filter_by(recipient_id=user.id, is_read=False).count()}


@router.post("/me/read")
def mark_notifications_read(user: User = Depends(current_user), db: Session = Depends(get_db)):
    db.query(Notification).filter_by(recipient_id=user.id, is_read=False).update({"is_read": True}); db.commit()
    return Response(status_code=204)
