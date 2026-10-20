"""Azure Service Bus helpers using the queue names and payload contract from .NET."""

from __future__ import annotations

import json
import os
import uuid
from datetime import datetime, timezone
from typing import Any


QUEUES = ("chat-messages", "post-notifications", "friend-requests", "verification-codes", "image-generation")


def enabled() -> bool:
    return bool(os.getenv("AZURE_SERVICEBUS_CONNECTION_STRING"))


def queue_name(queue: str) -> str:
    """Physical queue for a logical one. SERVICEBUS_QUEUE_PREFIX (e.g. "py-") keeps this backend from
    consuming the .NET Functions' messages when both share a namespace."""
    return os.getenv("SERVICEBUS_QUEUE_PREFIX", "") + queue


def publish(queue: str, payload: Any, message_id: str | None = None, subject: str | None = None) -> bool:
    """Publish a PascalCase .NET-compatible JSON message; returns False in local mode."""
    connection = os.getenv("AZURE_SERVICEBUS_CONNECTION_STRING")
    if not connection:
        return False
    from azure.servicebus import ServiceBusClient, ServiceBusMessage

    body = json.dumps(payload, separators=(",", ":"), default=str)
    with ServiceBusClient.from_connection_string(connection) as client:
        with client.get_queue_sender(queue_name(queue)) as sender:
            sender.send_messages(ServiceBusMessage(
                body,
                content_type="application/json",
                subject=subject or type(payload).__name__,
                message_id=message_id or uuid.uuid4().hex,
            ))
    return True


def notification_queue(kind: str) -> str:
    if kind in {"FriendRequestSent", "FriendRequestAccepted", "UserFollowed"}:
        return "friend-requests"
    return "post-notifications"


def publish_notification(kind: str, actor_id: int, recipient_id: int | None = None,
                         post_id: int | None = None, text: str | None = None) -> bool:
    event_id = uuid.uuid4()
    payload = {
        "EventId": str(event_id), "Type": kind, "ActorId": actor_id,
        "RecipientId": recipient_id, "PostId": post_id,
        "Text": text[:300] if text else None,
        "OccurredAt": datetime.now(timezone.utc).isoformat(),
    }
    return publish(notification_queue(kind), payload, event_id.hex, "NotificationEvent")
