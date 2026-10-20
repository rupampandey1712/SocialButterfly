"""/api/Post, /api/Likes and /api/Comment."""

from __future__ import annotations

from typing import Optional

from fastapi import APIRouter, Depends, File, Form, HTTPException, Response, UploadFile
from sqlalchemy.orm import Session, contains_eager, joinedload

from app.context import AppContext
from app.deps import current_user, get_ctx, get_db
from app.models import Comment, Like, Notification, Post, User
from app.schemas import CommentRequest
from app.services.identity_rules import clean_media_title
from app.services.media_rules import has_file, valid_media
from app.services.notifications import emit_notification
from app.views import author, post_views

router = APIRouter()


def visible_posts(db: Session):
    """Posts whose author is still active, newest first, with the author loaded by the same query."""
    return (db.query(Post).join(Post.user).options(contains_eager(Post.user))
            .filter(User.is_deleted.is_(False)).order_by(Post.created_at.desc(), Post.id.desc()))


@router.post("/api/Post")
def create_post(description: str = Form(""), musicTitle: str = Form(""), musicArtist: str = Form(""),
                image: Optional[UploadFile] = File(None), imageFile: Optional[UploadFile] = File(None),
                music: Optional[UploadFile] = File(None), musicFile: Optional[UploadFile] = File(None),
                user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    upload = image or imageFile; soundtrack = music or musicFile
    if not has_file(upload): upload = None
    if not has_file(soundtrack): soundtrack = None
    is_audio = bool(upload and valid_media(upload, "audio"))
    if upload and not is_audio and not valid_media(upload, "image,video"): raise HTTPException(400, "Posts can be photos, videos or songs (MP3, M4A, WAV, OGG, FLAC).")
    if upload and upload.size > 80 * 1024 * 1024: raise HTTPException(400, "Media files must be 80 MB or smaller.")
    if soundtrack and not valid_media(soundtrack, "audio"): raise HTTPException(400, "Music must be a supported audio file.")
    if soundtrack and (not upload or is_audio): raise HTTPException(400, "Music can only be added to a photo or video post.")
    if soundtrack and soundtrack.size > 25 * 1024 * 1024: raise HTTPException(400, "Music files must be 25 MB or smaller.")
    if not upload and not description.strip(): raise HTTPException(400, "A post needs a caption or a photo, video or song.")
    media = ctx.storage.save_upload(upload, "music" if is_audio else "posts") if upload else None
    music_path = ctx.storage.save_upload(soundtrack, "music") if soundtrack else None
    p = Post(user_id=user.id, image_path=media, description=description, music_path=music_path,
             music_title=clean_media_title(musicTitle, upload.filename if is_audio else None),
             music_artist=musicArtist.strip()[:120] or None)
    db.add(p); db.commit(); db.refresh(p)
    emit_notification(db, "PostCreated", user.id, post_id=p.id); db.commit()
    # "descriprion" (sic) is the .NET DTO's property name, which clients already read.
    return {"id": p.id, "imagePath": ctx.storage.public_url(p.image_path), "descriprion": p.description,
            "userId": p.user_id, "user": None, "createdAt": p.created_at,
            "musicPath": ctx.storage.public_url(p.music_path), "musicTitle": p.music_title,
            "musicArtist": p.music_artist, "comments": [], "likes": []}


@router.get("/api/Post/feed")
def feed(skip: int = 0, take: int = 20, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    posts = visible_posts(db).offset(max(skip, 0)).limit(min(max(take, 1), 50)).all()
    return post_views(db, ctx.storage, posts, user.id)


@router.get("/api/Post/UserPost")
def legacy_user_feed(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return post_views(db, ctx.storage, visible_posts(db).limit(50).all(), user.id)


@router.get("/api/Post")
def all_comments(user: User = Depends(current_user), db: Session = Depends(get_db)):
    rows = db.query(Comment).order_by(Comment.created_at.desc()).all()
    if not rows: return Response(status_code=204)
    return [{"id": c.id, "postId": c.post_id, "userId": c.user_id, "text": c.text, "createdAt": c.created_at, "user": None} for c in rows]


@router.get("/api/Post/user/{user_id:int}")
@router.get("/api/Post/{user_id:int}")
def user_posts(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return post_views(db, ctx.storage, visible_posts(db).filter(Post.user_id == user_id).all(), user.id)


@router.get("/api/Post/details/{post_id:int}")
def post_details(post_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Post, post_id)
    if not p: raise HTTPException(404)
    return post_views(db, ctx.storage, [p], user.id)[0]


@router.delete("/api/Post/{post_id:int}")
def delete_post(post_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Post, post_id)
    if not p: raise HTTPException(404)
    if p.user_id != user.id: raise HTTPException(403)
    image_path, music_path = p.image_path, p.music_path
    db.query(Comment).filter_by(post_id=post_id).delete()
    db.query(Like).filter_by(post_id=post_id).delete()
    db.query(Notification).filter_by(post_id=post_id).delete()
    db.delete(p); db.commit()
    ctx.storage.delete(image_path, "music" if image_path and "/music/" in image_path else "posts")
    ctx.storage.delete(music_path, "music")
    return Response(status_code=204)


@router.post("/api/Likes/toggle")
def toggle_like(postId: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    p = db.get(Post, postId)
    if not p: raise HTTPException(404)
    like = db.query(Like).filter_by(post_id=postId, user_id=user.id).first()
    db.query(Notification).filter_by(recipient_id=p.user_id, actor_id=user.id, post_id=postId, type="PostLiked").delete()
    if like: db.delete(like)
    else: db.add(Like(post_id=postId, user_id=user.id))
    liked = like is None
    db.commit()
    emit_notification(db, "PostLiked" if liked else "PostUnliked", user.id, p.user_id if liked else None, postId); db.commit()
    return {"postId": postId, "likeCount": db.query(Like).filter_by(post_id=postId).count(), "isLiked": liked}


@router.post("/api/Likes")
def like_count(postId: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if not db.get(Post, postId): raise HTTPException(400, "A valid user and post are required.")
    if not db.query(Like).filter_by(post_id=postId, user_id=user.id).first():
        db.add(Like(post_id=postId, user_id=user.id)); db.commit()
    return db.query(Like).filter_by(post_id=postId).count()


@router.post("/api/Comment")
def add_comment(body: CommentRequest, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Post, body.postId)
    if not p: raise HTTPException(400, "A valid user and post are required to create a comment.")
    if not body.text.strip(): raise HTTPException(400, "Comment text is required.")
    c = Comment(post_id=p.id, user_id=user.id, text=body.text); db.add(c); db.commit(); db.refresh(c)
    emit_notification(db, "PostCommented", user.id, p.user_id, p.id, c.text); db.commit()
    return {"id": c.id, "postId": c.post_id, "text": c.text, "createdAt": c.created_at, "user": author(user, ctx.storage)}


@router.get("/api/Comment/{post_id:int}")
def comments(post_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = db.query(Comment).options(joinedload(Comment.user)).filter_by(post_id=post_id).order_by(Comment.created_at)
    return [{"id": c.id, "postId": c.post_id, "text": c.text, "createdAt": c.created_at, "user": author(c.user, ctx.storage)} for c in rows]


@router.post("/api/Post/Comments")
def legacy_comment(body: CommentRequest, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    result = add_comment(body, user, ctx, db)
    return {"id": result["id"], "comment": result}
