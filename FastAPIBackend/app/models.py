"""ORM models. Schema changes go through Alembic migrations in app/migrations."""

from __future__ import annotations

import uuid
from datetime import datetime, timedelta
from typing import Optional

from sqlalchemy import BigInteger, Boolean, DateTime, ForeignKey, Index, Integer, String, Text, UniqueConstraint, text
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db import Base, utcnow

EMAIL_NOT_NULL = text("email IS NOT NULL")


class User(Base):
    __tablename__ = "users"
    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    name: Mapped[Optional[str]] = mapped_column(String(200))
    dob: Mapped[str] = mapped_column(String(40), default="0001-01-01T00:00:00")
    email: Mapped[Optional[str]] = mapped_column(String(320))
    password_hash: Mapped[str] = mapped_column(String(300))
    role: Mapped[str] = mapped_column(String(30), default="User")
    is_deleted: Mapped[bool] = mapped_column(Boolean, default=False)
    image_path: Mapped[Optional[str]] = mapped_column(String(1000))
    information: Mapped[Optional[str]] = mapped_column(Text)
    username: Mapped[Optional[str]] = mapped_column(String(30), unique=True, index=True)
    phone_number: Mapped[Optional[str]] = mapped_column(String(20), unique=True)
    theme: Mapped[str] = mapped_column(String(10), default="system")
    email_confirmed: Mapped[bool] = mapped_column(Boolean, default=False)
    phone_confirmed: Mapped[bool] = mapped_column(Boolean, default=False)
    mute_like_notifications: Mapped[bool] = mapped_column(Boolean, default=False)
    mute_comment_notifications: Mapped[bool] = mapped_column(Boolean, default=False)
    mute_follow_notifications: Mapped[bool] = mapped_column(Boolean, default=False)
    mute_new_post_notifications: Mapped[bool] = mapped_column(Boolean, default=False)
    # One account per email address; phone-only accounts leave it NULL.
    __table_args__ = (Index("ux_users_email", "email", unique=True, sqlite_where=EMAIL_NOT_NULL,
                            postgresql_where=EMAIL_NOT_NULL, mssql_where=EMAIL_NOT_NULL),)


class Post(Base):
    __tablename__ = "posts"
    id: Mapped[int] = mapped_column(primary_key=True)
    image_path: Mapped[Optional[str]] = mapped_column(String(1000))
    description: Mapped[Optional[str]] = mapped_column(Text)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow, index=True)
    music_path: Mapped[Optional[str]] = mapped_column(String(1000))
    music_title: Mapped[Optional[str]] = mapped_column(String(120))
    music_artist: Mapped[Optional[str]] = mapped_column(String(120))
    user: Mapped[User] = relationship()


class Comment(Base):
    __tablename__ = "comments"
    id: Mapped[int] = mapped_column(primary_key=True)
    post_id: Mapped[int] = mapped_column(ForeignKey("posts.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    text: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    user: Mapped[User] = relationship()


class Like(Base):
    __tablename__ = "likes"
    id: Mapped[int] = mapped_column(primary_key=True)
    post_id: Mapped[int] = mapped_column(ForeignKey("posts.id", ondelete="CASCADE"))
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (UniqueConstraint("post_id", "user_id"),)


class Friend(Base):
    __tablename__ = "friends"
    id: Mapped[int] = mapped_column(primary_key=True)
    sender_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    receiver_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    is_accepted: Mapped[bool] = mapped_column(Boolean, default=False)
    accepted_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)


class Follow(Base):
    __tablename__ = "follows"
    id: Mapped[int] = mapped_column(primary_key=True)
    follower_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    following_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (UniqueConstraint("follower_id", "following_id"),)


class ChatMessage(Base):
    __tablename__ = "chat_messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    message_id: Mapped[str] = mapped_column(String(36), default=lambda: str(uuid.uuid4()), unique=True)
    sender_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    receiver_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    content: Mapped[str] = mapped_column(String(2000))
    sent_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    delivered_at: Mapped[Optional[datetime]] = mapped_column(DateTime)
    is_read: Mapped[bool] = mapped_column(Boolean, default=False)


class Story(Base):
    __tablename__ = "stories"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    media_path: Mapped[str] = mapped_column(String(1000))
    caption: Mapped[Optional[str]] = mapped_column(String(200))
    music_path: Mapped[Optional[str]] = mapped_column(String(1000))
    music_title: Mapped[Optional[str]] = mapped_column(String(120))
    music_artist: Mapped[Optional[str]] = mapped_column(String(120))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(DateTime, default=lambda: utcnow() + timedelta(hours=24), index=True)


class StoryView(Base):
    __tablename__ = "story_views"
    id: Mapped[int] = mapped_column(primary_key=True)
    story_id: Mapped[int] = mapped_column(ForeignKey("stories.id", ondelete="CASCADE"))
    viewer_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    viewed_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (UniqueConstraint("story_id", "viewer_id"),)


class VerificationCode(Base):
    __tablename__ = "verification_codes"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    target: Mapped[str] = mapped_column(String(256), index=True)
    channel: Mapped[str] = mapped_column(String(10))
    purpose: Mapped[str] = mapped_column(String(10))
    code_hash: Mapped[str] = mapped_column(String(64))
    attempts: Mapped[int] = mapped_column(Integer, default=0)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(DateTime)
    consumed_at: Mapped[Optional[datetime]] = mapped_column(DateTime)


class GeneratedImage(Base):
    __tablename__ = "generated_images"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    prompt: Mapped[str] = mapped_column(String(800))
    style: Mapped[str] = mapped_column(String(30), default="none")
    width: Mapped[int] = mapped_column(Integer)
    height: Mapped[int] = mapped_column(Integer)
    seed: Mapped[int] = mapped_column(BigInteger)
    status: Mapped[str] = mapped_column(String(20), default="Queued")
    image_path: Mapped[Optional[str]] = mapped_column(String(1000))
    error: Mapped[Optional[str]] = mapped_column(String(300))
    post_id: Mapped[Optional[int]] = mapped_column(Integer)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    started_at: Mapped[Optional[datetime]] = mapped_column(DateTime)
    completed_at: Mapped[Optional[datetime]] = mapped_column(DateTime)


class Notification(Base):
    __tablename__ = "notifications"
    id: Mapped[int] = mapped_column(primary_key=True)
    recipient_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    actor_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    post_id: Mapped[Optional[int]] = mapped_column(ForeignKey("posts.id", ondelete="CASCADE"))
    event_id: Mapped[str] = mapped_column(String(36), default=lambda: str(uuid.uuid4()))
    type: Mapped[str] = mapped_column(String(40))
    text: Mapped[Optional[str]] = mapped_column(String(300))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    is_read: Mapped[bool] = mapped_column(Boolean, default=False)
    __table_args__ = (UniqueConstraint("event_id", "recipient_id"),)
