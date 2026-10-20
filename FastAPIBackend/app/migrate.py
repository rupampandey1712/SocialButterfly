"""Brings a database up to the latest Alembic revision when the application starts."""

from __future__ import annotations

from pathlib import Path

from alembic import command
from alembic.config import Config
from sqlalchemy import inspect
from sqlalchemy.engine import Connection, Engine

BASELINE = "0001"
MIGRATIONS = Path(__file__).resolve().parent / "migrations"

# Columns that earlier versions added with ALTER TABLE instead of a migration.
LEGACY_USER_COLUMNS = {
    "email_confirmed": "BOOLEAN NOT NULL DEFAULT 0", "phone_confirmed": "BOOLEAN NOT NULL DEFAULT 0",
    "mute_like_notifications": "BOOLEAN NOT NULL DEFAULT 0", "mute_comment_notifications": "BOOLEAN NOT NULL DEFAULT 0",
    "mute_follow_notifications": "BOOLEAN NOT NULL DEFAULT 0", "mute_new_post_notifications": "BOOLEAN NOT NULL DEFAULT 0",
}


def alembic_config(connection: Connection | None = None) -> Config:
    config = Config()
    config.set_main_option("script_location", str(MIGRATIONS))
    if connection is not None:
        config.attributes["connection"] = connection
    return config


def _adopt_legacy_database(connection: Connection) -> None:
    """A database made by create_all has every baseline table but no alembic_version: finish it, then stamp it."""
    existing = {column["name"] for column in inspect(connection).get_columns("users")}
    for name, ddl in LEGACY_USER_COLUMNS.items():
        if name not in existing:
            connection.exec_driver_sql(f"ALTER TABLE users ADD COLUMN {name} {ddl}")
    command.stamp(alembic_config(connection), BASELINE)


def upgrade_database(engine: Engine, revision: str = "head") -> None:
    with engine.begin() as connection:
        tables = set(inspect(connection).get_table_names())
        if "users" in tables and "alembic_version" not in tables:
            _adopt_legacy_database(connection)
        command.upgrade(alembic_config(connection), revision)
