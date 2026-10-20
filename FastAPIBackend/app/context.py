"""Per-application state shared by middleware, routes and background workers."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Awaitable, Callable

from sqlalchemy.engine import Engine
from sqlalchemy.orm import sessionmaker

from app.config import Settings
from app.security.bot_defense import BotDefense, ProofOfWork
from app.security.rate_limit import RateLimiter
from app.security.tokens import TokenService
from app.services.blob_storage import BlobStorage


@dataclass
class AppContext:
    settings: Settings
    engine: Engine
    SessionLocal: sessionmaker
    storage: BlobStorage
    tokens: TokenService
    bot_defense: BotDefense = field(default_factory=BotDefense)
    pow: ProofOfWork = None  # type: ignore[assignment]
    rate_limiter: RateLimiter = field(default_factory=RateLimiter)
    # Runs one image-generation job; replaced in tests.
    process_image: Callable[[int], Awaitable[None]] = None  # type: ignore[assignment]

    def __post_init__(self) -> None:
        if self.pow is None:
            self.pow = ProofOfWork(self.settings.pow_secret)
        if self.process_image is None:
            from app.services.images import process_image

            async def run(job_id: int) -> None:
                await process_image(self, job_id)

            self.process_image = run
