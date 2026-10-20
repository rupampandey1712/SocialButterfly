"""JSON request bodies. Field names mirror the .NET DTOs' JSON names, and keys bind case-insensitively
like System.Text.Json does there, so {"postId": 1} and {"PostId": 1} are the same request."""

from __future__ import annotations

import uuid
from typing import Any, Optional

from pydantic import BaseModel, ConfigDict, model_validator


class DotNetRequest(BaseModel):
    model_config = ConfigDict(extra="ignore")

    @model_validator(mode="before")
    @classmethod
    def _bind_case_insensitively(cls, data: Any) -> Any:
        if not isinstance(data, dict):
            return data
        names = {name.casefold(): name for name in cls.model_fields}
        bound: dict[str, Any] = {}
        for key, value in data.items():
            name = names.get(str(key).casefold())
            # A JSON null leaves the field at its default, as an absent property would.
            if name is not None and name not in bound and value is not None:
                bound[name] = value
        return bound


class CodeSendRequest(DotNetRequest):
    target: str = ""
    purpose: str = "Verify"
    website: Optional[str] = None


class CodeConfirmRequest(DotNetRequest):
    target: str = ""
    purpose: str = "Verify"
    code: str = ""
    newPassword: str = ""
    website: Optional[str] = None


class PasswordChangeRequest(DotNetRequest):
    currentPassword: str = ""
    newPassword: str = ""


class CommentRequest(DotNetRequest):
    postId: int = 0
    text: str = ""


class FriendRequestBody(DotNetRequest):
    receiverId: int = 0


class ChatSendRequest(DotNetRequest):
    receiverId: int = 0
    content: str = ""
    clientMessageId: Optional[uuid.UUID] = None


class ImageRequest(DotNetRequest):
    prompt: str = ""
    style: str = "none"
    aspect: str = "square"
    seed: Optional[int] = None


class ShareImageRequest(DotNetRequest):
    caption: Optional[str] = None


class GenAiRequest(DotNetRequest):
    prompt: str = ""
