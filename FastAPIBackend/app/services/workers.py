"""Background workers: Service Bus queue consumers and expired-story cleanup.

Each handler does its blocking work (SQLAlchemy, blob storage, SMTP) through asyncio.to_thread,
so the request-serving event loop stays responsive while a worker is busy.
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING

from sqlalchemy.exc import IntegrityError

from app.db import utcnow
from app.models import ChatMessage, Story, StoryView, User
from app.services import service_bus
from app.services.notifications import process_notification_event
from app.services.verification import deliver_code

if TYPE_CHECKING:
    from app.context import AppContext

log = logging.getLogger(__name__)

# Image jobs can run for GENERATION_TIMEOUT (10 min), far past the default 1-minute peek lock:
# keep renewing it so the message isn't redelivered to a second worker mid-job.
LOCK_RENEWAL_SECONDS = {"image-generation": 15 * 60}
BATCH_SIZE = {"image-generation": 1}
STORY_CLEANUP_INTERVAL = 15 * 60


def decode_body(message) -> dict:
    raw = message.body
    if isinstance(raw, str): body = raw
    elif isinstance(raw, (bytes, bytearray)): body = bytes(raw).decode("utf-8")
    else: body = b"".join(bytes(part) for part in raw).decode("utf-8")
    return json.loads(body)


def persist_chat_message(ctx: "AppContext", event: dict) -> None:
    msg_id = str(uuid.UUID(str(event["MessageId"])))
    with ctx.SessionLocal() as session:
        if session.query(ChatMessage).filter_by(message_id=msg_id).first():
            return
        sender_id = int(event["SenderId"]); receiver_id = int(event["ReceiverId"]); content = str(event["Content"]).strip()
        active = session.query(User).filter(User.id.in_([sender_id, receiver_id]), User.is_deleted.is_(False)).count()
        if sender_id <= 0 or receiver_id <= 0 or sender_id == receiver_id or not content or len(content) > 2000 or active != 2:
            raise ValueError("The queued chat message is invalid or its participants are inactive.")
        sent = datetime.fromisoformat(str(event["SentAt"]).replace("Z", "+00:00")).replace(tzinfo=None)
        session.add(ChatMessage(message_id=msg_id, sender_id=sender_id, receiver_id=receiver_id, content=content,
                                sent_at=sent, delivered_at=utcnow()))
        try:
            session.commit()
        except IntegrityError:
            session.rollback()
            if not session.query(ChatMessage).filter_by(message_id=msg_id).first(): raise


def apply_notification(ctx: "AppContext", event: dict) -> None:
    with ctx.SessionLocal() as session:
        process_notification_event(session, event)


async def handle_message(ctx: "AppContext", queue: str, event: dict) -> None:
    """Process one message from a logical queue; raising abandons the message for redelivery."""
    if queue == "chat-messages":
        await asyncio.to_thread(persist_chat_message, ctx, event)
    elif queue in {"post-notifications", "friend-requests"}:
        await asyncio.to_thread(apply_notification, ctx, event)
    elif queue == "verification-codes":
        expires = datetime.fromisoformat(str(event["ExpiresAt"]).replace("Z", "+00:00"))
        if expires > datetime.now(timezone.utc):
            await asyncio.to_thread(deliver_code, event["Channel"], event["Target"], event["Purpose"], event["Code"], expires.replace(tzinfo=None))
    elif queue == "image-generation":
        await ctx.process_image(int(event["JobId"]))
    else:
        raise ValueError(f"Unknown queue: {queue}")


async def service_bus_worker(ctx: "AppContext", queue: str) -> None:
    from azure.servicebus.aio import AutoLockRenewer, ServiceBusClient

    connection = os.environ["AZURE_SERVICEBUS_CONNECTION_STRING"]
    renew_for = LOCK_RENEWAL_SECONDS.get(queue)
    while True:
        try:
            async with ServiceBusClient.from_connection_string(connection) as client, AutoLockRenewer() as renewer:
                async with client.get_queue_receiver(queue_name=service_bus.queue_name(queue), max_wait_time=5) as receiver:
                    while True:
                        messages = await receiver.receive_messages(max_message_count=BATCH_SIZE.get(queue, 10), max_wait_time=5)
                        for message in messages:
                            if renew_for:
                                renewer.register(receiver, message, max_lock_renewal_duration=renew_for)
                            try:
                                await handle_message(ctx, queue, decode_body(message))
                                await receiver.complete_message(message)
                            except Exception:
                                log.exception("Processing a %s message failed; it will be redelivered.", queue)
                                await receiver.abandon_message(message)
        except asyncio.CancelledError:
            raise
        except Exception:
            log.exception("The %s consumer lost its Service Bus connection; reconnecting.", queue)
            await asyncio.sleep(5)


def purge_expired_stories(ctx: "AppContext") -> int:
    with ctx.SessionLocal() as session:
        expired = session.query(Story).filter(Story.expires_at <= utcnow()).order_by(Story.expires_at).limit(500).all()
        for story in expired:
            ctx.storage.delete(story.media_path, "stories")
            ctx.storage.delete(story.music_path, "music")
            session.query(StoryView).filter_by(story_id=story.id).delete()
            session.delete(story)
        session.commit()
        return len(expired)


async def story_cleanup_worker(ctx: "AppContext") -> None:
    while True:
        try:
            await asyncio.to_thread(purge_expired_stories, ctx)
        except Exception:
            log.exception("Expired-story cleanup failed; retrying next cycle.")
        await asyncio.sleep(STORY_CLEANUP_INTERVAL)


def start_workers(ctx: "AppContext") -> list[asyncio.Task]:
    tasks = [asyncio.create_task(story_cleanup_worker(ctx))]
    if service_bus.enabled() and ctx.settings.worker_mode == "python":
        tasks.extend(asyncio.create_task(service_bus_worker(ctx, queue)) for queue in service_bus.QUEUES)
    return tasks
