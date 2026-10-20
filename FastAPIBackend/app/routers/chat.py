"""/api/Chat: direct messages, published to Service Bus for the Functions/worker pipeline."""

from __future__ import annotations

import uuid
from datetime import timezone

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import JSONResponse
from sqlalchemy import and_, or_
from sqlalchemy.orm import Session

from app.db import utcnow
from app.deps import current_user, get_db
from app.models import ChatMessage, User
from app.schemas import ChatSendRequest
from app.services import service_bus

router = APIRouter(prefix="/api/Chat")


@router.post("")
def send_message(body: ChatSendRequest, user: User = Depends(current_user), db: Session = Depends(get_db)):
    receiver = body.receiverId; content = body.content.strip()
    if not content or len(content) > 2000:
        return JSONResponse({"error": "A chat message must contain between 1 and 2000 characters."}, status_code=400)
    recipient = db.get(User, receiver)
    if receiver == user.id or not recipient or recipient.is_deleted:
        return JSONResponse({"error": "Sender and receiver must be different valid users."}, status_code=400)
    message_id = str(body.clientMessageId or uuid.uuid4())
    msg = db.query(ChatMessage).filter_by(message_id=message_id).first()
    if not msg:
        msg = ChatMessage(message_id=message_id, sender_id=user.id, receiver_id=receiver, content=content, delivered_at=utcnow())
        db.add(msg); db.commit(); db.refresh(msg)
        try:
            service_bus.publish("chat-messages", {"MessageId": msg.message_id, "SenderId": msg.sender_id, "ReceiverId": msg.receiver_id,
                                                  "Content": msg.content, "SentAt": msg.sent_at.replace(tzinfo=timezone.utc).isoformat()},
                                msg.message_id.replace("-", ""), "ChatQueueMessage")
        except Exception:
            pass  # Already stored; the queue only feeds the realtime/Functions pipeline.
    return JSONResponse({"messageId": msg.message_id, "status": "Queued"}, status_code=202)


@router.get("/with/{other_id:int}")
def conversation(other_id: int, limit: int = 50, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if other_id <= 0 or other_id == user.id: raise HTTPException(400, "Conversation users must be different valid users.")
    between = or_(and_(ChatMessage.sender_id == user.id, ChatMessage.receiver_id == other_id),
                  and_(ChatMessage.sender_id == other_id, ChatMessage.receiver_id == user.id))
    rows = db.query(ChatMessage).filter(between).order_by(ChatMessage.sent_at.desc(), ChatMessage.id.desc()).limit(min(max(limit, 1), 100)).all()
    return [{"id": m.id, "messageId": m.message_id, "senderId": m.sender_id, "receiverId": m.receiver_id, "content": m.content,
             "sentAt": m.sent_at, "deliveredAt": m.delivered_at, "isRead": m.is_read} for m in reversed(rows)]


@router.put("/with/{other_id:int}/read")
def mark_read(other_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    count = db.query(ChatMessage).filter_by(sender_id=other_id, receiver_id=user.id, is_read=False).update({"is_read": True})
    db.commit()
    return {"markedRead": count}
