"""Baseline: the schema the backend created with metadata.create_all before migrations existed.

Revision ID: 0001
Revises:
Create Date: 2026-10-18
"""

import sqlalchemy as sa
from alembic import op

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "users",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("name", sa.String(200)),
        sa.Column("dob", sa.String(40), nullable=False),
        sa.Column("email", sa.String(320)),
        sa.Column("password_hash", sa.String(300), nullable=False),
        sa.Column("role", sa.String(30), nullable=False),
        sa.Column("is_deleted", sa.Boolean, nullable=False),
        sa.Column("image_path", sa.String(1000)),
        sa.Column("information", sa.Text),
        sa.Column("username", sa.String(30)),
        sa.Column("phone_number", sa.String(20), unique=True),
        sa.Column("theme", sa.String(10), nullable=False),
        sa.Column("email_confirmed", sa.Boolean, nullable=False),
        sa.Column("phone_confirmed", sa.Boolean, nullable=False),
        sa.Column("mute_like_notifications", sa.Boolean, nullable=False),
        sa.Column("mute_comment_notifications", sa.Boolean, nullable=False),
        sa.Column("mute_follow_notifications", sa.Boolean, nullable=False),
        sa.Column("mute_new_post_notifications", sa.Boolean, nullable=False),
    )
    op.create_index("ix_users_email", "users", ["email"])
    op.create_index("ix_users_username", "users", ["username"], unique=True)
    op.create_table(
        "posts",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("image_path", sa.String(1000)),
        sa.Column("description", sa.Text),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("music_path", sa.String(1000)),
        sa.Column("music_title", sa.String(120)),
        sa.Column("music_artist", sa.String(120)),
    )
    op.create_table(
        "comments",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("post_id", sa.Integer, sa.ForeignKey("posts.id", ondelete="CASCADE"), nullable=False),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("text", sa.Text, nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
    )
    op.create_table(
        "likes",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("post_id", sa.Integer, sa.ForeignKey("posts.id", ondelete="CASCADE"), nullable=False),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.UniqueConstraint("post_id", "user_id"),
    )
    op.create_table(
        "friends",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("sender_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("receiver_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("is_accepted", sa.Boolean, nullable=False),
        sa.Column("accepted_at", sa.DateTime, nullable=False),
    )
    op.create_table(
        "follows",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("follower_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("following_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.UniqueConstraint("follower_id", "following_id"),
    )
    op.create_table(
        "chat_messages",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("message_id", sa.String(36), nullable=False, unique=True),
        sa.Column("sender_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("receiver_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("content", sa.String(2000), nullable=False),
        sa.Column("sent_at", sa.DateTime, nullable=False),
        sa.Column("delivered_at", sa.DateTime),
        sa.Column("is_read", sa.Boolean, nullable=False),
    )
    op.create_table(
        "stories",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("media_path", sa.String(1000), nullable=False),
        sa.Column("caption", sa.String(200)),
        sa.Column("music_path", sa.String(1000)),
        sa.Column("music_title", sa.String(120)),
        sa.Column("music_artist", sa.String(120)),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("expires_at", sa.DateTime, nullable=False),
    )
    op.create_table(
        "story_views",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("story_id", sa.Integer, sa.ForeignKey("stories.id", ondelete="CASCADE"), nullable=False),
        sa.Column("viewer_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("viewed_at", sa.DateTime, nullable=False),
        sa.UniqueConstraint("story_id", "viewer_id"),
    )
    op.create_table(
        "verification_codes",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
        sa.Column("target", sa.String(256), nullable=False),
        sa.Column("channel", sa.String(10), nullable=False),
        sa.Column("purpose", sa.String(10), nullable=False),
        sa.Column("code_hash", sa.String(64), nullable=False),
        sa.Column("attempts", sa.Integer, nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("expires_at", sa.DateTime, nullable=False),
        sa.Column("consumed_at", sa.DateTime),
    )
    op.create_table(
        "generated_images",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("user_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("prompt", sa.String(800), nullable=False),
        sa.Column("style", sa.String(30), nullable=False),
        sa.Column("width", sa.Integer, nullable=False),
        sa.Column("height", sa.Integer, nullable=False),
        sa.Column("seed", sa.BigInteger, nullable=False),
        sa.Column("status", sa.String(20), nullable=False),
        sa.Column("image_path", sa.String(1000)),
        sa.Column("error", sa.String(300)),
        sa.Column("post_id", sa.Integer),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("started_at", sa.DateTime),
        sa.Column("completed_at", sa.DateTime),
    )
    op.create_table(
        "notifications",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("recipient_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("actor_id", sa.Integer, sa.ForeignKey("users.id"), nullable=False),
        sa.Column("post_id", sa.Integer, sa.ForeignKey("posts.id", ondelete="CASCADE")),
        sa.Column("event_id", sa.String(36), nullable=False),
        sa.Column("type", sa.String(40), nullable=False),
        sa.Column("text", sa.String(300)),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("is_read", sa.Boolean, nullable=False),
        sa.UniqueConstraint("event_id", "recipient_id"),
    )


def downgrade() -> None:
    for table in ("notifications", "generated_images", "verification_codes", "story_views", "stories",
                  "chat_messages", "follows", "friends", "likes", "comments", "posts", "users"):
        op.drop_table(table)
