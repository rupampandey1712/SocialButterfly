"""Session tokens: HS256 JWTs with the same claims the .NET backend issues."""

from __future__ import annotations

from datetime import datetime, timedelta, timezone
from typing import Optional

import jwt


class TokenService:
    ALGORITHM = "HS256"

    def __init__(self, secret: str, issuer: str, audience: str, minutes: int) -> None:
        self._secret = secret
        self.issuer = issuer
        self.audience = audience
        self.lifetime = timedelta(minutes=minutes)

    def issue(self, user_id: int, role: str) -> str:
        expires = datetime.now(timezone.utc) + self.lifetime
        claims = {"sub": str(user_id), "role": role, "iss": self.issuer, "aud": self.audience, "exp": expires}
        return jwt.encode(claims, self._secret, algorithm=self.ALGORITHM)

    def user_id(self, token: Optional[str]) -> Optional[int]:
        """The user id of a valid, unexpired token from this issuer for this audience, or None."""
        if not token:
            return None
        try:
            claims = jwt.decode(token, self._secret, algorithms=[self.ALGORITHM], audience=self.audience,
                                issuer=self.issuer, options={"require": ["exp", "iss", "aud", "sub"]})
            return int(claims["sub"])
        except (jwt.PyJWTError, ValueError):
            return None
