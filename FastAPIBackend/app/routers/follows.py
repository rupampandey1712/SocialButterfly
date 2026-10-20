"""/api/Follows: follow toggles, follower lists, people search and suggestions."""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.context import AppContext
from app.deps import current_user, get_ctx, get_db
from app.models import Follow, User
from app.security.search_text import contains_pattern
from app.services.notifications import emit_notification
from app.views import counts_by, follow_cards, users_by_id

router = APIRouter(prefix="/api/Follows")


@router.post("/{target_id:int}/toggle")
def toggle_follow(target_id: int, user: User = Depends(current_user), db: Session = Depends(get_db)):
    if target_id == user.id: raise HTTPException(400, "Cannot follow yourself")
    target = db.get(User, target_id)
    if not target or target.is_deleted: raise HTTPException(404)
    row = db.query(Follow).filter_by(follower_id=user.id, following_id=target_id).first()
    if row: db.delete(row)
    else: db.add(Follow(follower_id=user.id, following_id=target_id))
    db.commit()
    following = row is None
    if following: emit_notification(db, "UserFollowed", user.id, target_id); db.commit()
    return {"isFollowing": following, "followersCount": db.query(Follow).filter_by(following_id=target_id).count()}


def follow_list(db: Session, ctx: AppContext, user_id: int, followers: bool, viewer_id: int) -> list[dict]:
    match, other = (Follow.following_id, Follow.follower_id) if followers else (Follow.follower_id, Follow.following_id)
    ids = list(db.scalars(select(other).where(match == user_id).order_by(Follow.created_at.desc(), Follow.id.desc())))
    users = users_by_id(db, ids)
    return follow_cards(db, ctx.storage, [users[i] for i in ids if i in users and not users[i].is_deleted], viewer_id)


@router.get("/{user_id:int}/followers")
def followers(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return follow_list(db, ctx, user_id, True, user.id)


@router.get("/{user_id:int}/following")
def following(user_id: int, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    return follow_list(db, ctx, user_id, False, user.id)


@router.get("/people")
def people(q: str = "", take: int = 60, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    query = db.query(User).filter(User.is_deleted.is_(False), User.role != "Admin")
    if q and q.strip():
        pattern = contains_pattern(q.strip().lstrip("@"))
        query = query.filter(or_(User.name.ilike(pattern, escape="\\"), User.username.ilike(pattern, escape="\\"),
                                 User.information.ilike(pattern, escape="\\")))
    followers_count = select(func.count(Follow.id)).where(Follow.following_id == User.id).correlate(User).scalar_subquery()
    users = query.order_by(followers_count.desc(), User.id.desc()).limit(min(max(take, 1), 100)).all()
    return follow_cards(db, ctx.storage, users, user.id)


@router.get("/suggestions")
def suggestions(take: int = 5, user: User = Depends(current_user), ctx: AppContext = Depends(get_ctx), db: Session = Depends(get_db)):
    mine = set(db.scalars(select(Follow.following_id).where(Follow.follower_id == user.id)))
    candidates = db.query(User).filter(User.id != user.id, User.is_deleted.is_(False), User.role != "Admin", ~User.id.in_(mine or {-1})).all()
    ids = {u.id for u in candidates}
    # Rank: people who follow you, then people followed by those you follow, then popularity.
    follows_you = set(db.scalars(select(Follow.follower_id).where(Follow.following_id == user.id)))
    mutual = dict(db.execute(select(Follow.following_id, func.count()).where(Follow.follower_id.in_(mine), Follow.following_id.in_(ids))
                             .group_by(Follow.following_id)).all()) if mine and ids else {}
    popularity = counts_by(db, Follow.following_id, ids)
    candidates.sort(key=lambda u: (-(3 * (u.id in follows_you) + mutual.get(u.id, 0)), -popularity.get(u.id, 0), -u.id))
    return follow_cards(db, ctx.storage, candidates[:min(max(take, 1), 30)], user.id, is_following=set())
