"""Lookup indexes for feeds, lists and workers; one account per email address.

Revision ID: 0002
Revises: 0001
Create Date: 2026-10-18
"""

import sqlalchemy as sa
from alembic import op

revision = "0002"
down_revision = "0001"
branch_labels = None
depends_on = None

EMAIL_NOT_NULL = sa.text("email IS NOT NULL")
INDEXES = (
    ("posts", "user_id"), ("posts", "created_at"), ("comments", "post_id"),
    ("friends", "sender_id"), ("friends", "receiver_id"), ("follows", "following_id"),
    ("chat_messages", "sender_id"), ("chat_messages", "receiver_id"),
    ("stories", "user_id"), ("stories", "expires_at"), ("verification_codes", "target"),
    ("generated_images", "user_id"), ("notifications", "recipient_id"),
)


def upgrade() -> None:
    connection = op.get_bind()
    # The old PUT /api/Account stored emails as typed; logins compare normalized (lower-case) values.
    connection.execute(sa.text("UPDATE users SET email = lower(trim(email)) WHERE email IS NOT NULL"))
    connection.execute(sa.text("UPDATE users SET email = NULL WHERE email = ''"))
    duplicates = connection.execute(sa.text(
        "SELECT email FROM users WHERE email IS NOT NULL GROUP BY email HAVING COUNT(*) > 1")).scalars().all()
    if duplicates:
        raise RuntimeError(
            "Cannot add the unique email index: these emails belong to more than one account: "
            + ", ".join(duplicates) + ". Change or remove the extra accounts, then restart.")
    op.drop_index("ix_users_email", table_name="users")
    op.create_index("ux_users_email", "users", ["email"], unique=True, sqlite_where=EMAIL_NOT_NULL,
                    postgresql_where=EMAIL_NOT_NULL, mssql_where=EMAIL_NOT_NULL)
    for table, column in INDEXES:
        op.create_index(f"ix_{table}_{column}", table, [column])


def downgrade() -> None:
    for table, column in INDEXES:
        op.drop_index(f"ix_{table}_{column}", table_name=table)
    op.drop_index("ux_users_email", table_name="users")
    op.create_index("ix_users_email", "users", ["email"])
