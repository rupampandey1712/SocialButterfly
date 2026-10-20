"""/api/Account: registration, sign-in, one-time codes, settings and member lookup."""

from __future__ import annotations

import hashlib
import hmac
import math
import secrets
import uuid
from datetime import datetime, timedelta, timezone
from typing import Optional

from fastapi import APIRouter, Depends, File, Form, HTTPException, Request, Response, UploadFile
from sqlalchemy import and_, or_
from sqlalchemy.orm import Session

from app.context import AppContext
from app.db import utcnow
from app.deps import SESSION_COOKIE, bot_challenge, current_user, get_ctx, get_db, request_ip, set_session_cookie
from app.models import Follow, Friend, Post, Story, User, VerificationCode
from app.schemas import CodeConfirmRequest, CodeSendRequest, PasswordChangeRequest
from app.security.bot_defense import BotDefense
from app.security.passwords import check_password, hash_password
from app.security.search_text import contains_pattern
from app.services import service_bus
from app.services.blob_storage import BlobStorage
from app.services.identity_rules import normalize_email, normalize_phone, normalize_username, username_seed
from app.services.media_rules import has_file, valid_media
from app.services.verification import deliver_code
from app.views import EMPTY_DATE

router = APIRouter(prefix="/api/Account")

PHOTO_RULE = "Profile photos must be images (JPG, PNG, GIF, WebP or AVIF)."
USERNAME_RULE = "Usernames can only use letters, numbers, underscores and periods (3-30 characters)."
PURPOSES = {"Verify", "Login", "Reset"}


def mask_contact(target: str) -> str:
    if "@" in target:
        local, domain = target.split("@", 1)
        visible = local[:1] if len(local) <= 2 else local[:2]
        return f"{visible}{'*' * max(3, len(local) - len(visible))}@{domain}"
    return target if len(target) <= 6 else f"{target[:3]}{'*' * (len(target) - 7)}{target[-4:]}"


def normalize_dob(value: Optional[str]) -> str:
    if not value or not value.strip(): return EMPTY_DATE
    raw = value.strip()
    try:
        return datetime.fromisoformat(raw.replace("Z", "+00:00")).isoformat()
    except ValueError:
        for pattern in ("%d/%m/%Y", "%m/%d/%Y"):
            try: return datetime.strptime(raw, pattern).isoformat()
            except ValueError: pass
    raise HTTPException(400, "The value for Date of Birth is not valid.")


def suggest_username(db: Session, seed: str) -> str:
    if not db.query(User).filter_by(username=seed).first():
        return seed
    for attempt in range(20):
        upper = 999 if attempt < 10 else 99999
        candidate = f"{seed}{secrets.randbelow(upper - 10) + 10}"
        if not db.query(User).filter_by(username=candidate).first():
            return candidate
    return f"{seed}{uuid.uuid4().hex[:6]}"


def require_photo(upload: Optional[UploadFile]) -> None:
    if has_file(upload) and not valid_media(upload, "image"):
        raise HTTPException(400, PHOTO_RULE)


def replace_photo(storage: BlobStorage, user: User, upload: Optional[UploadFile]) -> None:
    require_photo(upload)
    if not has_file(upload): return
    previous = user.image_path
    user.image_path = storage.save_upload(upload, "Images")
    if previous: storage.delete(previous, "Images")


def change_email(db: Session, user: User, raw: str) -> None:
    """Normalize, validate and claim an email; a new address must be verified again."""
    normalized = normalize_email(raw)
    if raw.strip() and not normalized: raise HTTPException(400, "Enter a valid email address.")
    if normalized and db.query(User).filter(User.email == normalized, User.id != user.id).first():
        raise HTTPException(400, "Another account is using the same email.")
    if normalized != user.email:
        user.email = normalized; user.email_confirmed = False


def login_view(user: User, storage: BlobStorage) -> dict:
    return {"id": user.id, "email": user.email, "role": user.role, "token": None,
            "imagePath": storage.public_url(user.image_path), "name": user.name,
            "username": user.username, "theme": user.theme}


def settings_view(user: User, storage: BlobStorage) -> dict:
    return {"id": user.id, "name": user.name, "username": user.username, "email": user.email, "dob": user.dob,
            "imagePath": storage.public_url(user.image_path), "phoneNumber": user.phone_number,
            "emailConfirmed": user.email_confirmed, "phoneConfirmed": user.phone_confirmed,
            "information": user.information, "theme": user.theme,
            "notifyLikes": not user.mute_like_notifications, "notifyComments": not user.mute_comment_notifications,
            "notifyFollows": not user.mute_follow_notifications, "notifyNewPosts": not user.mute_new_post_notifications}


@router.post("/register")
def register(request: Request, name: str = Form(""), email: str = Form(""), password: str = Form(""), dob: str = Form(""),
             username: str = Form(""), phoneNumber: str = Form(""), information: str = Form(""), website: str = Form(""),
             imageFile: Optional[UploadFile] = File(None), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    if website.strip():
        ctx.bot_defense.flag_bot(request_ip(request)); raise HTTPException(400, "We couldn't create your account. Please try again.")
    if challenge := bot_challenge(ctx, request, "register", None): return challenge
    raw_email = email; raw_phone = phoneNumber; raw_username = username
    email = normalize_email(email); username = normalize_username(username) if username.strip() else None; phoneNumber = normalize_phone(phoneNumber)
    if raw_email.strip() and email is None: raise HTTPException(400, "Enter a valid email address.")
    if not password.strip() or len(password) < 8 or not (email or phoneNumber):
        raise HTTPException(400, "Provide a name, email or mobile number, and a password at least 8 characters long.")
    if raw_phone.strip() and phoneNumber is None: raise HTTPException(400, "Enter a valid mobile number with country code.")
    if raw_username.strip() and username is None: raise HTTPException(400, USERNAME_RULE)
    require_photo(imageFile)
    if not username:
        username = suggest_username(db, username_seed(name, email))
    taken = or_(User.email == email if email else False, User.username == username,
                User.phone_number == phoneNumber if phoneNumber else False)
    if db.query(User).filter(taken).first(): raise HTTPException(400, "Another account is using these details.")
    user = User(name=name.strip() or username, email=email, username=username, phone_number=phoneNumber, dob=normalize_dob(dob),
                information=information or None, password_hash=hash_password(password), role="User",
                image_path=ctx.storage.save_upload(imageFile, "Images") if has_file(imageFile) else None)
    db.add(user); db.commit(); db.refresh(user)
    return {"id": user.id, "username": user.username, "verificationTarget": user.email or user.phone_number}


@router.post("/login")
def login(request: Request, response: Response, email: str = Form(""), identifier: str = Form(""), password: str = Form(""),
          username: str = Form(""), website: str = Form(""), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    key = BotDefense.account_key(identifier or email); ip = request_ip(request)
    wrong = "Sorry, your password was incorrect. Please double-check your password."
    if website.strip():
        ctx.bot_defense.record_failure(ip, key); ctx.bot_defense.flag_bot(ip); raise HTTPException(401, wrong)
    if challenge := bot_challenge(ctx, request, "login", key): return challenge
    identity = (identifier or email or username).strip()
    if not identity or not password.strip(): raise HTTPException(401, "Enter your phone number, username or email, and your password.")
    identity = normalize_email(identity) or normalize_phone(identity) or normalize_username(identity) or identity.lower()
    user = db.query(User).filter(or_(User.email == identity, User.username == identity, User.phone_number == identity)).first()
    if not user or user.is_deleted or not check_password(password, user.password_hash):
        ctx.bot_defense.record_failure(ip, key); raise HTTPException(401, wrong)
    ctx.bot_defense.record_success(ip, key)
    set_session_cookie(response, ctx, user)
    return login_view(user, ctx.storage)


@router.get("/session")
def session(request: Request, ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    try: user = current_user(request, ctx, db)
    except HTTPException: return {"authenticated": False}
    return {"authenticated": True, "id": user.id, "name": user.name, "username": user.username,
            "imagePath": ctx.storage.public_url(user.image_path), "theme": user.theme, "role": user.role}


@router.post("/logout")
def logout(ctx: AppContext = Depends(get_ctx)):
    response = Response(status_code=204)
    response.delete_cookie(SESSION_COOKIE, path="/", secure=ctx.settings.cookie_secure, httponly=True, samesite="strict")
    return response


@router.get("/availability")
def availability(email: Optional[str] = None, username: Optional[str] = None, phone: Optional[str] = None,
                 name: Optional[str] = None, db: Session = Depends(get_db)):
    result = {}
    if email is not None:
        normalized = normalize_email(email)
        result["emailAvailable"] = bool(normalized and not db.query(User).filter(User.email == normalized).first())
    if username is not None:
        normalized = normalize_username(username)
        result["usernameAvailable"] = bool(normalized and not db.query(User).filter(User.username == normalized).first())
        if not result["usernameAvailable"]:
            result["suggestedUsername"] = suggest_username(db, username_seed(normalized or username, None))
    elif name:
        result["suggestedUsername"] = suggest_username(db, username_seed(name, email))
    if phone is not None:
        normalized = normalize_phone(phone)
        result["phoneAvailable"] = bool(normalized and not db.query(User).filter(User.phone_number == normalized).first())
    return result


def normalize_target(raw: str) -> tuple[bool, str]:
    """(is_email, normalized target) for an email address or mobile number."""
    email = normalize_email(raw)
    target = email or normalize_phone(raw)
    if not target: raise HTTPException(400, "Enter a valid email address or mobile number with country code.")
    return email is not None, target


@router.post("/code/send")
def send_code(request: Request, body: CodeSendRequest, ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    target = body.target.strip(); purpose = body.purpose
    if body.website:
        ctx.bot_defense.flag_bot(request_ip(request)); raise HTTPException(400, "We couldn't send a code right now.")
    if challenge := bot_challenge(ctx, request, "code-send", BotDefense.account_key(target)): return challenge
    if purpose not in PURPOSES or not target: raise HTTPException(400, "Unknown verification purpose.")
    email, target = normalize_target(target)
    channel = "Email" if email else "Sms"
    account = db.query(User).filter(User.is_deleted.is_(False), User.email == target if email else User.phone_number == target).first()
    if not account:
        if purpose == "Verify": raise HTTPException(404, "We couldn't find an account with that contact.")
        # Same answer as a real send, so the endpoint can't be used to discover accounts.
        return {"channel": channel, "destination": mask_contact(target), "expiresAt": utcnow() + timedelta(minutes=10), "devCode": None}
    if purpose == "Verify" and ((email and account.email_confirmed) or (not email and account.phone_confirmed)):
        raise HTTPException(400, "This contact method is already verified.")
    code = f"{secrets.randbelow(1000000):06d}"; now = utcnow(); expires = now + timedelta(minutes=10)
    recent = (db.query(VerificationCode).filter_by(target=target, purpose=purpose)
              .filter(VerificationCode.created_at > now - timedelta(hours=1)).order_by(VerificationCode.created_at.desc()).all())
    if recent and (now - recent[0].created_at).total_seconds() < 30:
        raise HTTPException(400, f"Please wait {math.ceil(30 - (now - recent[0].created_at).total_seconds())} seconds before requesting another code.")
    if len(recent) >= 5: raise HTTPException(400, "Too many codes requested. Please try again later.")
    for old in recent:
        if old.consumed_at is None: old.consumed_at = now
    db.add(VerificationCode(user_id=account.id, target=target, channel=channel, purpose=purpose, created_at=now, expires_at=expires,
                            code_hash=hashlib.sha256(f"{target}:{code}".encode()).hexdigest()))
    db.commit()
    expose = ctx.settings.expose_dev_codes
    if service_bus.enabled():
        event_id = uuid.uuid4()
        try:
            service_bus.publish("verification-codes", {"EventId": str(event_id), "Channel": channel, "Target": target, "Purpose": purpose,
                                                       "Code": code, "ExpiresAt": expires.replace(tzinfo=timezone.utc).isoformat()},
                                event_id.hex, "VerificationCodeMessage")
        except Exception as exc:
            if not expose: raise HTTPException(400, "We couldn't send your code right now. Please try again.") from exc
    elif not expose:
        try: deliver_code(channel, target, purpose, code, expires)
        except Exception as exc: raise HTTPException(400, f"We couldn't send your code right now. {str(exc)[:180]}")
    return {"channel": channel, "destination": mask_contact(target), "expiresAt": expires, "devCode": code if expose else None}


@router.post("/code/confirm")
def confirm_code(request: Request, body: CodeConfirmRequest, response: Response, ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    target = body.target.strip(); purpose = body.purpose; ip = request_ip(request)
    account_key = BotDefense.account_key(target)
    invalid = "That code isn't valid. Check it and try again."
    if body.website:
        ctx.bot_defense.flag_bot(ip); ctx.bot_defense.record_failure(ip, account_key); raise HTTPException(401, invalid)
    if challenge := bot_challenge(ctx, request, "code-confirm", account_key): return challenge
    if purpose not in PURPOSES: raise HTTPException(400, "Unknown verification purpose.")
    _, target = normalize_target(target)
    row = (db.query(VerificationCode).filter_by(target=target, purpose=purpose, consumed_at=None)
           .order_by(VerificationCode.created_at.desc()).first())
    account = db.get(User, row.user_id) if row else None
    if not row or row.expires_at <= utcnow() or not account or account.is_deleted:
        raise HTTPException(400, "That code has expired. Request a new one.")
    if row.attempts >= 5: raise HTTPException(400, "Too many incorrect attempts. Request a new code.")
    if not hmac.compare_digest(row.code_hash, hashlib.sha256(f"{row.target}:{body.code}".encode()).hexdigest()):
        row.attempts += 1; db.commit()
        ctx.bot_defense.record_failure(ip, account_key); raise HTTPException(401, invalid)
    row.consumed_at = utcnow()
    if row.channel == "Email": account.email_confirmed = True
    else: account.phone_confirmed = True
    if purpose == "Verify":
        db.commit(); ctx.bot_defense.record_success(ip, account_key); return {"verified": True}
    if purpose == "Reset":
        if not body.newPassword.strip() or len(body.newPassword) < 8: raise HTTPException(400, "Create a password at least 8 characters long.")
        account.password_hash = hash_password(body.newPassword)
    db.commit(); ctx.bot_defense.record_success(ip, account_key)
    set_session_cookie(response, ctx, account)
    return login_view(account, ctx.storage)


@router.get("/me/settings")
def settings(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx)):
    return settings_view(user, ctx.storage)


@router.put("/me/settings")
def update_settings(name: Optional[str] = Form(None), username: Optional[str] = Form(None), email: Optional[str] = Form(None),
                    phoneNumber: Optional[str] = Form(None), information: Optional[str] = Form(None), theme: Optional[str] = Form(None),
                    notifyLikes: Optional[bool] = Form(None), notifyComments: Optional[bool] = Form(None),
                    notifyFollows: Optional[bool] = Form(None), notifyNewPosts: Optional[bool] = Form(None),
                    imageFile: Optional[UploadFile] = File(None), user: User = Depends(current_user),
                    ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    require_photo(imageFile)
    if name is not None and name.strip(): user.name = name.strip()
    if username is not None:
        normalized = normalize_username(username)
        if not normalized: raise HTTPException(400, USERNAME_RULE)
        if db.query(User).filter(User.username == normalized, User.id != user.id).first(): raise HTTPException(400, "This username isn't available.")
        user.username = normalized
    if email is not None: change_email(db, user, email)
    if phoneNumber is not None:
        normalized = normalize_phone(phoneNumber)
        if phoneNumber.strip() and not normalized: raise HTTPException(400, "Enter a valid mobile number with country code.")
        if normalized and db.query(User).filter(User.phone_number == normalized, User.id != user.id).first():
            raise HTTPException(400, "Another account is using the same mobile number.")
        if normalized != user.phone_number: user.phone_number = normalized; user.phone_confirmed = False
    if user.email is None and user.phone_number is None: raise HTTPException(400, "Keep at least an email address or a mobile number on your account.")
    if information is not None: user.information = information[:150]
    if theme is not None:
        if theme not in ("light", "dark", "system"): raise HTTPException(400, "Theme must be light, dark or system.")
        user.theme = theme
    for attr, value in (("mute_like_notifications", notifyLikes), ("mute_comment_notifications", notifyComments),
                        ("mute_follow_notifications", notifyFollows), ("mute_new_post_notifications", notifyNewPosts)):
        if value is not None: setattr(user, attr, not value)
    replace_photo(ctx.storage, user, imageFile)
    db.commit()
    return settings_view(user, ctx.storage)


@router.put("/me/password")
def change_password(body: PasswordChangeRequest, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if not check_password(body.currentPassword, user.password_hash): raise HTTPException(400, "Your current password is incorrect.")
    if not body.newPassword.strip() or len(body.newPassword) < 8: raise HTTPException(400, "Create a password at least 8 characters long.")
    user.password_hash = hash_password(body.newPassword); db.commit()
    return Response(status_code=204)


@router.put("")
def legacy_update(id: int = Form(...), name: Optional[str] = Form(None), dob: Optional[str] = Form(None), email: Optional[str] = Form(None),
                  information: Optional[str] = Form(None), imageFile: Optional[UploadFile] = File(None), user: User = Depends(current_user),
                  ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    if user.id != id and user.role != "Admin": raise HTTPException(403)
    target = db.get(User, id)
    if not target: raise HTTPException(400, f"User with id {id} not found")
    require_photo(imageFile)
    if name is not None: target.name = name
    if dob: target.dob = normalize_dob(dob)
    # The legacy form only ever sets an email; clearing one goes through /me/settings.
    if email and email.strip(): change_email(db, target, email)
    if information is not None: target.information = information
    replace_photo(ctx.storage, target, imageFile)
    db.commit()
    return settings_view(target, ctx.storage)


@router.get("/Get")
def get_users(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    admin = user.role == "Admin"
    return [{"id": u.id, "name": u.name, "imagePath": ctx.storage.public_url(u.image_path), "email": u.email if admin else None,
             "dob": u.dob if admin else EMPTY_DATE, "role": u.role} for u in db.query(User).all()]


@router.get("/Search")
def search(Name: str = "", Email: str = "", Role: str = "", Information: str = "", user: User = Depends(current_user),
           ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    users = db.query(User).filter(User.is_deleted.is_(False), User.role != "Admin")
    terms = {v.strip().lower() for v in (Name, Email, Role, Information) if v.strip()}
    like = lambda column, value: column.ilike(contains_pattern(value), escape="\\")
    if len(terms) == 1:
        q = next(iter(terms))
        users = users.filter(or_(like(User.name, q), like(User.email, q), like(User.username, q.lstrip("@")), like(User.information, q)))
    else:
        if Name: users = users.filter(like(User.name, Name))
        if Email: users = users.filter(like(User.email, Email))
        if Role: users = users.filter(like(User.role, Role))
        if Information: users = users.filter(like(User.information, Information))
    admin = user.role == "Admin"
    return [{"id": u.id, "name": u.name, "username": u.username, "imagePath": ctx.storage.public_url(u.image_path),
             "information": u.information, "email": u.email if admin else None, "role": u.role} for u in users]


@router.get("/{user_id:int}/profile")
@router.get("/{user_id:int}")
def profile(user_id: int, viewer: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    u = db.get(User, user_id)
    if not u or u.is_deleted: raise HTTPException(404)
    friendship = db.query(Friend).filter(or_(and_(Friend.sender_id == viewer.id, Friend.receiver_id == user_id),
                                             and_(Friend.sender_id == user_id, Friend.receiver_id == viewer.id))).first()
    status = ("Self" if viewer.id == user_id else "Friends" if friendship and friendship.is_accepted
              else "RequestSent" if friendship and friendship.sender_id == viewer.id else "RequestReceived" if friendship else "None")
    return {"id": u.id, "name": u.name, "username": u.username, "imagePath": ctx.storage.public_url(u.image_path), "information": u.information,
            "followersCount": db.query(Follow).filter_by(following_id=user_id).count(),
            "followingCount": db.query(Follow).filter_by(follower_id=user_id).count(),
            "isFollowing": db.query(Follow).filter_by(follower_id=viewer.id, following_id=user_id).first() is not None,
            "followsYou": db.query(Follow).filter_by(follower_id=user_id, following_id=viewer.id).first() is not None,
            "hasActiveStory": db.query(Story).filter(Story.user_id == user_id, Story.expires_at > utcnow()).first() is not None,
            "postsCount": db.query(Post).filter_by(user_id=user_id).count(),
            "friendsCount": db.query(Friend).filter(Friend.is_accepted.is_(True), or_(Friend.sender_id == user_id, Friend.receiver_id == user_id)).count(),
            "friendshipStatus": status}
