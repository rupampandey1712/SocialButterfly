"""Password hashing: bcrypt (work factor 12), still verifying the older PBKDF2 hashes."""

from __future__ import annotations

import hashlib
import hmac

import bcrypt


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode(), bcrypt.gensalt(rounds=12)).decode()


def check_password(password: str, stored: str) -> bool:
    if stored.startswith(("$2a$", "$2b$", "$2y$")):
        try:
            return bcrypt.checkpw(password.encode(), stored.encode())
        except ValueError:
            return False
    salt, _, digest = stored.partition("$")
    return bool(digest) and hmac.compare_digest(
        hashlib.pbkdf2_hmac("sha256", password.encode(), salt.encode(), 260000).hex(), digest)
