"""/api/Stories: 24-hour stories, viewers and the story tray."""

from __future__ import annotations

from typing import Optional

from fastapi import APIRouter, Depends, File, Form, HTTPException, Response, UploadFile
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.context import AppContext
from app.db import utcnow
from app.deps import current_user, get_ctx, get_db
from app.models import Follow, Story, StoryView, User
from app.services.identity_rules import clean_media_title
from app.services.media_rules import has_file, valid_media
from app.views import author, counts_by, friend_ids, story_view

router = APIRouter(prefix="/api/Stories")


def story_groups(db: Session, ctx: AppContext, stories: list[Story], viewer_id: int) -> dict[int, dict]:
    """Stories grouped by owner, with seen flags and (for the viewer's own stories) view counts."""
    ids = [s.id for s in stories]
    seen = set(db.scalars(select(StoryView.story_id).where(StoryView.viewer_id == viewer_id, StoryView.story_id.in_(ids)))) if ids else set()
    views = counts_by(db, StoryView.story_id, [s.id for s in stories if s.user_id == viewer_id])
    owners = {u.id: u for u in db.scalars(select(User).where(User.id.in_({s.user_id for s in stories})))} if stories else {}
    groups: dict[int, dict] = {}
    for s in stories:
        owner = owners[s.user_id]; own = s.user_id == viewer_id
        item = story_view(s, ctx.storage, seen=own or s.id in seen, view_count=views.get(s.id, 0) if own else 0)
        group = groups.setdefault(s.user_id, {"user": author(owner, ctx.storage), "username": owner.username, "isOwn": own,
                                              "hasUnseen": False, "latestAt": s.created_at, "stories": []})
        group["stories"].append(item); group["hasUnseen"] |= not item["seen"]; group["latestAt"] = max(group["latestAt"], s.created_at)
    return groups


@router.get("/feed")
def story_feed(user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    author_ids = {user.id, *friend_ids(db, user.id)}
    author_ids.update(db.scalars(select(Follow.following_id).where(Follow.follower_id == user.id)))
    stories = (db.query(Story).join(User, User.id == Story.user_id)
               .filter(Story.user_id.in_(author_ids), Story.expires_at > utcnow(), User.is_deleted.is_(False))
               .order_by(Story.created_at).all())
    groups = story_groups(db, ctx, stories, user.id)
    return sorted(groups.values(), key=lambda g: (not g["isOwn"], not g["hasUnseen"], -g["latestAt"].timestamp()))


@router.post("")
def create_story(media: Optional[UploadFile] = File(None), caption: str = Form(""), music: Optional[UploadFile] = File(None),
                 musicTitle: str = Form(""), musicArtist: str = Form(""), user: User = Depends(current_user),
                 ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    if not has_file(media): raise HTTPException(400, "Choose a photo or video for your story.")
    if not valid_media(media, "image,video"): raise HTTPException(400, "Stories can only be photos or videos.")
    if media.size > 50 * 1024 * 1024: raise HTTPException(400, "Story media must be 50 MB or smaller.")
    if not has_file(music): music = None
    if music and not valid_media(music, "audio"): raise HTTPException(400, "Music must be a supported audio file.")
    if music and music.size > 25 * 1024 * 1024: raise HTTPException(400, "Music files must be 25 MB or smaller.")
    path = ctx.storage.save_upload(media, "stories")
    music_path = ctx.storage.save_upload(music, "music") if music else None
    s = Story(user_id=user.id, media_path=path, caption=caption.strip()[:200] or None, music_path=music_path,
              music_title=clean_media_title(musicTitle, music.filename if music else None),
              music_artist=clean_media_title(musicArtist) if music else None)
    db.add(s); db.commit(); db.refresh(s)
    return story_view(s, ctx.storage, seen=True)


@router.get("/user/{user_id:int}")
def user_stories(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    owner = db.get(User, user_id)
    if not owner or owner.is_deleted: return []
    stories = db.query(Story).filter(Story.user_id == user_id, Story.expires_at > utcnow()).order_by(Story.created_at).all()
    return list(story_groups(db, ctx, stories, user.id).values())


@router.post("/{story_id:int}/view")
def view_story(story_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    s = db.get(Story, story_id)
    if s and s.user_id != user.id and not db.query(StoryView).filter_by(story_id=story_id, viewer_id=user.id).first():
        db.add(StoryView(story_id=story_id, viewer_id=user.id)); db.commit()
    return Response(status_code=204)


@router.get("/{story_id:int}/viewers")
def story_viewers(story_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    s = db.get(Story, story_id)
    if not s: raise HTTPException(404)
    if s.user_id != user.id: raise HTTPException(403)
    rows = db.execute(select(StoryView, User).join(User, User.id == StoryView.viewer_id)
                      .where(StoryView.story_id == story_id).order_by(StoryView.viewed_at.desc())).all()
    return [{"user": author(viewer, ctx.storage), "viewedAt": view.viewed_at} for view, viewer in rows]


@router.delete("/{story_id:int}")
def delete_story(story_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    s = db.get(Story, story_id)
    if not s: raise HTTPException(404)
    if s.user_id != user.id: raise HTTPException(403)
    db.query(StoryView).filter_by(story_id=story_id).delete()
    ctx.storage.delete(s.media_path, "stories"); ctx.storage.delete(s.music_path, "music")
    db.delete(s); db.commit()
    return Response(status_code=204)
