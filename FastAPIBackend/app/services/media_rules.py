"""Upload checks ported from the .NET MediaRules: extension, declared MIME type and, for images, the file signature."""

from __future__ import annotations

from pathlib import Path
from typing import Optional

from fastapi import UploadFile

IMAGE_TYPES = {".jpg": "image/", ".jpeg": "image/", ".png": "image/", ".gif": "image/", ".webp": "image/", ".avif": "image/"}
VIDEO_TYPES = {".mp4": "video/", ".webm": "video/", ".mov": "video/", ".m4v": "video/", ".ogv": "video/"}
AUDIO_TYPES = {".mp3": "audio/", ".m4a": "audio/", ".aac": "audio/", ".wav": "audio/", ".ogg": "audio/",
               ".oga": "audio/", ".opus": "audio/", ".flac": "audio/", ".weba": "audio/"}
KINDS = {"image": IMAGE_TYPES, "image,video": {**IMAGE_TYPES, **VIDEO_TYPES}, "audio": AUDIO_TYPES}


def has_file(upload: Optional[UploadFile]) -> bool:
    return bool(upload and upload.filename and upload.size)


def looks_like_image(header: bytes) -> bool:
    """True when the bytes start like a JPEG, PNG, GIF, WebP or AVIF file (never SVG, HTML or script)."""
    return (header.startswith(b"\xff\xd8\xff") or header.startswith(b"\x89PNG\r\n\x1a\n")
            or header[:6] in (b"GIF87a", b"GIF89a") or (header[:4] == b"RIFF" and header[8:12] == b"WEBP")
            or (header[4:8] == b"ftyp" and header[8:12] in (b"avif", b"avis", b"mif1", b"msf1")))


def _read_header(upload: UploadFile) -> bytes:
    upload.file.seek(0)
    header = upload.file.read(16)
    upload.file.seek(0)
    return header


def valid_media(upload: UploadFile, kind: str) -> bool:
    """The extension and the declared MIME type must both be allowed for `kind`; image files must also really be images."""
    allowed = KINDS[kind]
    ext = Path(upload.filename or "").suffix.lower()
    mime = (upload.content_type or "").lower()
    if ext not in allowed or not mime.startswith(allowed[ext]):
        return False
    return ext not in IMAGE_TYPES or looks_like_image(_read_header(upload))
