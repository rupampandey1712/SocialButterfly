"""Normalization rules ported from the .NET IdentityRules and MediaRules."""

from __future__ import annotations

import re
from pathlib import Path
from typing import Optional


def normalize_email(value: Optional[str]) -> Optional[str]:
    if not value or not value.strip(): return None
    value=value.strip().lower()
    if value.count("@") != 1 or any(c.isspace() for c in value): return None
    local, domain=value.split("@",1)
    return value if local and domain and "." in value else None


def normalize_phone(value: Optional[str]) -> Optional[str]:
    if not value or not value.strip(): return None
    raw=value.strip()
    if not 8 <= len(raw) <= 20 or any(c not in "+0123456789 ()-." for c in raw): return None
    value="".join(c for c in raw if c.isdigit() or c=="+")
    if value.startswith("00"): value="+"+value[2:]
    return value if re.fullmatch(r"\+[1-9]\d{7,14}",value) else None


def normalize_username(value: Optional[str]) -> Optional[str]:
    if not value or not value.strip(): return None
    value=value.strip().lstrip("@").lower()
    return value if re.fullmatch(r"[a-z0-9._]{3,30}",value) and not value.startswith(".") and not value.endswith(".") else None


def username_seed(name: Optional[str], email: Optional[str]) -> str:
    source=name.strip() if name and name.strip() else (email or "member").split("@",1)[0]
    value="".join(c.lower() if c.isascii() and c.isalnum() else "." if c in " -" else "" for c in source)
    value=re.sub(r"\.{2,}",".",value).strip(".")
    if len(value)<3: value="member"+value
    return value[:24].rstrip(".")


def clean_media_title(value: Optional[str], filename: Optional[str] = None) -> Optional[str]:
    title=(value or "").strip()
    if not title and filename: title=Path(filename).stem.replace("_"," ").strip()
    return title[:120] or None
