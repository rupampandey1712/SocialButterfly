"""Alembic environment.

The app runs migrations itself at startup (app/migrate.py passes an open connection).
From the command line, `alembic upgrade head` uses DATABASE_URL or the default SQLite file.
"""

from alembic import context

from app import models  # noqa: F401  (registers every table on Base.metadata)
from app.config import Settings
from app.db import Base, make_engine

config = context.config


def run(connection) -> None:
    context.configure(connection=connection, target_metadata=Base.metadata, render_as_batch=True)
    with context.begin_transaction():
        context.run_migrations()


if context.is_offline_mode():
    raise SystemExit("Offline (--sql) migrations are not supported; run against a database.")

connection = config.attributes.get("connection")
if connection is not None:
    run(connection)
else:
    with make_engine(Settings.from_env().database_url).begin() as connection:
        run(connection)
