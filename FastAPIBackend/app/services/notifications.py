"""Notification fan-out, shared by request handlers (local mode) and the Service Bus worker."""

from __future__ import annotations

from datetime import datetime
from typing import Iterable, Optional

from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.db import utcnow
from app.models import Follow, Like, Notification, Post, User
from app.services import service_bus
from app.views import friend_ids

MUTE_FIELDS = {
    "PostLiked": "mute_like_notifications", "PostCommented": "mute_comment_notifications",
    "PostCreated": "mute_new_post_notifications", "UserFollowed": "mute_follow_notifications",
    "FriendRequestSent": "mute_follow_notifications", "FriendRequestAccepted": "mute_follow_notifications",
}


def audience(db: Session, actor: int) -> set[int]:
    """Friends and followers of `actor`: who hears about a new post."""
    ids = set(friend_ids(db, actor))
    ids.update(db.scalars(select(Follow.follower_id).where(Follow.following_id == actor)))
    return ids


def eligible_recipients(db: Session, recipients: Iterable[int], actor: int, kind: str) -> set[int]:
    """Active recipients, other than the actor, who haven't muted this kind of notification (one query)."""
    field = MUTE_FIELDS.get(kind)
    ids = {r for r in recipients if r != actor}
    if not field or not ids:
        return set()
    return set(db.scalars(select(User.id).where(User.id.in_(ids), User.is_deleted.is_(False), getattr(User, field).is_(False))))


def process_notification_event(db: Session, event: dict) -> None:
    event_id = str(event.get("EventId", "")); kind = event.get("Type"); actor = int(event.get("ActorId", 0))
    post_id = event.get("PostId"); post_id = int(post_id) if post_id is not None else None
    text = event.get("Text"); occurred = event.get("OccurredAt")
    if not event_id or actor <= 0:
        raise ValueError("Invalid notification event")
    if kind == "PostUnliked":
        db.query(Notification).filter_by(type="PostLiked", actor_id=actor, post_id=post_id).delete(); db.commit(); return
    if kind == "PostLiked":
        if not db.query(Like).filter_by(user_id=actor, post_id=post_id).first(): return
        post = db.get(Post, post_id) if post_id else None
        recipients = {post.user_id} if post else set()
    elif kind == "PostCommented":
        post = db.get(Post, post_id) if post_id else None
        recipients = {post.user_id} if post else set()
    elif kind == "PostCreated":
        recipients = audience(db, actor)
    elif kind in {"FriendRequestSent", "FriendRequestAccepted", "UserFollowed"}:
        target = event.get("RecipientId"); recipients = {int(target)} if target is not None else set()
    else:
        raise ValueError(f"Unknown notification type: {kind}")
    if kind == "PostLiked":
        db.query(Notification).filter_by(type="PostLiked", actor_id=actor, post_id=post_id).delete()
    recipients = eligible_recipients(db, recipients, actor, kind)
    created = utcnow()
    if occurred:
        try: created = datetime.fromisoformat(str(occurred).replace("Z", "+00:00")).replace(tzinfo=None)
        except ValueError: pass
    already = set(db.scalars(select(Notification.recipient_id).where(Notification.event_id == event_id, Notification.recipient_id.in_(recipients)))) if recipients else set()
    for rid in recipients - already:
        db.add(Notification(event_id=event_id, recipient_id=rid, actor_id=actor, post_id=post_id, type=kind,
                            text=text[:300] if text else None, created_at=created))
    try:
        db.commit()
    except IntegrityError:
        # A redelivered event raced with its first delivery; the rows already exist.
        db.rollback()
        if not db.query(Notification).filter_by(event_id=event_id).first(): raise


def emit_notification(db: Session, kind: str, actor: int, recipient: Optional[int] = None,
                      post_id: Optional[int] = None, text: Optional[str] = None) -> None:
    """Queue the event on Service Bus, or (local mode, or if publishing fails) add the rows to `db` directly.
    The caller commits."""
    if service_bus.enabled():
        try:
            if service_bus.publish_notification(kind, actor, recipient, post_id, text): return
        except Exception:
            pass
    if recipient is not None:
        targets = {recipient}
    elif kind == "PostCreated":
        targets = audience(db, actor)
    else:
        return
    targets = eligible_recipients(db, targets, actor, kind)
    if kind == "PostLiked" and targets:
        targets -= set(db.scalars(select(Notification.recipient_id).where(
            Notification.recipient_id.in_(targets), Notification.actor_id == actor,
            Notification.post_id == post_id, Notification.type == kind)))
    for target in targets:
        db.add(Notification(recipient_id=target, actor_id=actor, type=kind, post_id=post_id, text=text))
