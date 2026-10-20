"""Response shapes shared by several routers. List builders load related rows in a fixed number of
queries (one per kind of data) instead of a few queries per item."""

from __future__ import annotations

from typing import Iterable, Optional, Sequence

from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.models import Comment, Follow, Friend, Like, Post, Story, User
from app.services.blob_storage import BlobStorage

EMPTY_DATE = "0001-01-01T00:00:00"


def dotnet_value(value):
    """`Ok(ActionResult<T>)` in the .NET controllers serializes the wrapper; the frontend reads `.value`."""
    return {"result": None, "value": value}


def author(user: User, storage: BlobStorage) -> dict:
    """Public identity only: never email, phone, role or credentials."""
    return {"id": user.id, "name": user.name, "imagePath": storage.public_url(user.image_path)}


def counts_by(db: Session, column, ids: Iterable[int]) -> dict[int, int]:
    ids = set(ids)
    if not ids:
        return {}
    return dict(db.execute(select(column, func.count()).where(column.in_(ids)).group_by(column)).all())


def users_by_id(db: Session, ids: Iterable[int]) -> dict[int, User]:
    ids = set(ids)
    return {u.id: u for u in db.scalars(select(User).where(User.id.in_(ids)))} if ids else {}


def post_views(db: Session, storage: BlobStorage, posts: Sequence[Post], viewer_id: int) -> list[dict]:
    ids = [p.id for p in posts]
    likes = counts_by(db, Like.post_id, ids)
    comments = counts_by(db, Comment.post_id, ids)
    liked = set(db.scalars(select(Like.post_id).where(Like.post_id.in_(ids), Like.user_id == viewer_id))) if ids else set()
    authors = users_by_id(db, {p.user_id for p in posts})
    return [{"id": p.id, "imagePath": storage.public_url(p.image_path), "description": p.description, "createdAt": p.created_at,
             "user": author(authors[p.user_id], storage) if p.user_id in authors else None,
             "likeCount": likes.get(p.id, 0), "commentCount": comments.get(p.id, 0), "isLiked": p.id in liked,
             "musicPath": storage.public_url(p.music_path), "musicTitle": p.music_title, "musicArtist": p.music_artist}
            for p in posts]


def follow_cards(db: Session, storage: BlobStorage, users: Sequence[User], viewer_id: int,
                 is_following: Optional[set[int]] = None) -> list[dict]:
    ids = {u.id for u in users}
    if is_following is None:
        is_following = set(db.scalars(select(Follow.following_id).where(Follow.follower_id == viewer_id, Follow.following_id.in_(ids)))) if ids else set()
    follows_you = set(db.scalars(select(Follow.follower_id).where(Follow.following_id == viewer_id, Follow.follower_id.in_(ids)))) if ids else set()
    followers = counts_by(db, Follow.following_id, ids)
    return [{"id": u.id, "name": u.name, "username": u.username, "imagePath": storage.public_url(u.image_path),
             "information": u.information, "isFollowing": u.id in is_following, "followsYou": u.id in follows_you,
             "isSelf": u.id == viewer_id, "followersCount": followers.get(u.id, 0)} for u in users]


def accepted_friendships(user_id: int):
    return select(Friend).where(Friend.is_accepted.is_(True), or_(Friend.sender_id == user_id, Friend.receiver_id == user_id))


def friend_ids(db: Session, user_id: int) -> list[int]:
    return [f.receiver_id if f.sender_id == user_id else f.sender_id for f in db.scalars(accepted_friendships(user_id))]


def active_friends(db: Session, user_id: int) -> list[User]:
    """Accepted friends who are still active, in friendship order."""
    ids = friend_ids(db, user_id)
    users = users_by_id(db, ids)
    return [users[i] for i in ids if i in users and not users[i].is_deleted]


def story_view(s: Story, storage: BlobStorage, seen: bool = False, view_count: int = 0) -> dict:
    return {"id": s.id, "mediaPath": storage.public_url(s.media_path), "caption": s.caption,
            "musicPath": storage.public_url(s.music_path), "musicTitle": s.music_title, "musicArtist": s.music_artist,
            "createdAt": s.created_at, "expiresAt": s.expires_at, "seen": seen, "viewCount": view_count}
