"""Fixed-window rate limiting, matching the .NET "auth", "codes" and "ai" policies."""

from __future__ import annotations

import threading
import time
from typing import Callable


class RateLimiter:
    """Counts requests per key in the current window.

    All keys share one window, so the whole table is dropped when the window rolls over:
    memory never holds more than one window's worth of keys, however many clients there are.
    """

    def __init__(self, window_seconds: int = 60, clock: Callable[[], float] = time.time) -> None:
        self._window_seconds = window_seconds
        self._clock = clock
        self._lock = threading.Lock()
        self._window = -1
        self._counts: dict[str, int] = {}

    def allow(self, key: str, limit: int) -> bool:
        window = int(self._clock() // self._window_seconds)
        with self._lock:
            if window != self._window:
                self._window = window
                self._counts.clear()
            count = self._counts.get(key, 0)
            if count >= limit:
                return False
            self._counts[key] = count + 1
            return True

    def __len__(self) -> int:
        with self._lock:
            return len(self._counts)
