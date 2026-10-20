"""/api/Friends: requests, acceptance and friend lists."""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import and_, or_
from sqlalchemy.orm import Session

from app.context import AppContext
from app.db import utcnow
from app.deps import current_user, get_ctx, get_db
from app.models import Friend, User
from app.schemas import FriendRequestBody
from app.services.notifications import emit_notification
from app.views import EMPTY_DATE, accepted_friendships, active_friends, author, dotnet_value, users_by_id

router = APIRouter(prefix="/api/Friends")


def between(a: int, b: int):
    return or_(and_(Friend.sender_id == a, Friend.receiver_id == b), and_(Friend.sender_id == b, Friend.receiver_id == a))


def friend_dto(row: Friend) -> dict:
    return {"id": row.id, "senderId": row.sender_id, "receiverId": row.receiver_id, "isAccepted": row.is_accepted,
            "acceptedAt": row.accepted_at if row.is_accepted else EMPTY_DATE}


def request_user(other: User, ctx: AppContext) -> dict:
    """The .NET RequestDto: public fields plus date of birth."""
    return {"id": other.id, "name": other.name, "dob": other.dob, "imagePath": ctx.storage.public_url(other.image_path)}


def accept(db: Session, user: User, sender_id: int) -> dict:
    row = db.query(Friend).filter_by(sender_id=sender_id, receiver_id=user.id, is_accepted=False).first()
    if not row: raise HTTPException(400, "Friend request not found.")
    row.is_accepted = True; row.accepted_at = utcnow(); db.commit()
    emit_notification(db, "FriendRequestAccepted", user.id, sender_id); db.commit()
    return friend_dto(row)


@router.post("")
def friend_request(body: FriendRequestBody, user: User = Depends(current_user), db: Session = Depends(get_db)):
    receiver = body.receiverId
    target = db.get(User, receiver)
    if receiver == user.id or not target or target.is_deleted: raise HTTPException(400, "Both users must exist and be active.")
    if db.query(Friend).filter(between(user.id, receiver)).first(): raise HTTPException(400, "A friend request or friendship already exists.")
    row = Friend(sender_id=user.id, receiver_id=receiver); db.add(row); db.commit(); db.refresh(row)
    emit_notification(db, "FriendRequestSent", user.id, receiver); db.commit()
    return friend_dto(row)


@router.post("/{sender_id:int}")
def accept_friend(sender_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    return accept(db, user, sender_id)


@router.post("/{sender_id:int}/{receiver_id:int}")
def accept_friend_pair(sender_id: int, receiver_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if receiver_id != user.id: raise HTTPException(403)
    return accept(db, user, sender_id)


@router.delete("/{sender_id:int}/{receiver_id:int}")
def remove_friend(sender_id: int, receiver_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if user.id not in (sender_id, receiver_id): raise HTTPException(403)
    row = db.query(Friend).filter(between(sender_id, receiver_id)).first()
    if row: db.delete(row); db.commit()
    return {"success": row is not None}


@router.get("/status/{other_id:int}")
def friend_status(other_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if other_id == user.id: return {"status": "None"}
    row = db.query(Friend).filter(between(user.id, other_id)).first()
    return {"status": "Friends" if row and row.is_accepted else "RequestSent" if row and row.sender_id == user.id
            else "RequestReceived" if row else "None"}


@router.get("/me/requests")
def incoming_count(user: User = Depends(current_user), db: Session = Depends(get_db)):
    return dotnet_value(db.query(Friend).filter_by(receiver_id=user.id, is_accepted=False).count())


@router.get("/me/requestuser")
def incoming_requests(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    sender_ids = [f.sender_id for f in db.query(Friend).filter_by(receiver_id=user.id, is_accepted=False)]
    senders = users_by_id(db, sender_ids)
    return dotnet_value([request_user(senders[i], ctx) for i in sender_ids if i in senders and not senders[i].is_deleted])


@router.get("/{user_id:int}/friends")
def friends(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    target = db.get(User, user_id)
    if not target or target.is_deleted: raise HTTPException(400, "User not found")
    return [author(other, ctx.storage) for other in active_friends(db, user_id)]


@router.get("/{user_id:int}/myfriends")
def my_friends(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return dotnet_value([request_user(other, ctx) for other in active_friends(db, user_id)])


@router.get("/{user_id:int}/friendscount")
def friends_count(user_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    return dotnet_value(db.query(accepted_friendships(user_id).subquery()).count())
