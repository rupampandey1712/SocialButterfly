"""Database primitives shared by the models, migrations and request handlers."""

from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import create_engine
from sqlalchemy.engine import Engine
from sqlalchemy.orm import DeclarativeBase


class Base(DeclarativeBase):
    pass


def utcnow() -> datetime:
    """Current UTC time as a naive datetime: every timestamp is stored as naive UTC, like the .NET schema."""
    return datetime.now(timezone.utc).replace(tzinfo=None)


def make_engine(url: str) -> Engine:
    return create_engine(url, connect_args={"check_same_thread": False} if url.startswith("sqlite") else {})
