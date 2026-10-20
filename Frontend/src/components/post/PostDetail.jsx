import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { MoreHoriz } from "@mui/icons-material";
import PostMedia from "./PostMedia";
import PostActions from "./PostActions";
import PostOptions from "./PostOptions";
import { usePostLike } from "./usePostLike";
import { addComment, fetchComments } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { timeAgo } from "../../utils/time";
import { showToast } from "../toast/Toaster";
import "./post.css";

function CommentRow({ user, text, createdAt, className = "", style }) {
  return (
    <div className={`commentRow ${className}`} style={style}>
      <Link to={user ? `/profile/${user.id}` : "#"}>
        <img
          className="igAvatar"
          width={32}
          height={32}
          src={getPublicImageUrl(user?.imagePath)}
          alt=""
          onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
        />
      </Link>
      <div className="commentBody">
        <p>
          <Link to={user ? `/profile/${user.id}` : "#"} className="postCaptionName">{user?.name || "User"}</Link>{" "}
          {text}
        </p>
        <time dateTime={createdAt}>{timeAgo(createdAt)}</time>
      </div>
    </div>
  );
}

/** Instagram's post view: media on the left, header + comments + actions on the right. */
export default function PostDetail({ post, onUpdate, onDelete }) {
  const [comments, setComments] = useState(null);
  const [newComment, setNewComment] = useState("");
  const [isPosting, setIsPosting] = useState(false);
  const [showOptions, setShowOptions] = useState(false);
  const inputRef = useRef(null);
  const listRef = useRef(null);
  const like = usePostLike(post, onUpdate);
  const author = post.user;

  useEffect(() => {
    let isActive = true;
    fetchComments(post.id)
      .then((data) => isActive && setComments(data))
      .catch(() => isActive && setComments([]));
    return () => { isActive = false; };
  }, [post.id]);

  const submitComment = async (event) => {
    event.preventDefault();
    const text = newComment.trim();
    const { id: userId } = getCurrentUser();
    if (!text || !userId || isPosting) return;
    setIsPosting(true);
    try {
      const created = await addComment(post.id, userId, text);
      setComments((prev) => [...(prev || []), { ...created, isNew: true }]);
      setNewComment("");
      onUpdate({ ...post, commentCount: post.commentCount + 1 });
      requestAnimationFrame(() => {
        listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: "smooth" });
      });
    } catch {
      showToast("Couldn't post comment");
    } finally {
      setIsPosting(false);
    }
  };

  return (
    <div className={`postDetail ${post.imagePath ? "" : "noMedia"}`}>
      {post.imagePath && (
        <div className="postDetailMedia">
          <PostMedia post={post} onDoubleTap={() => like({ onlyLike: true })} className="isContained" />
        </div>
      )}

      <div className="postDetailSide">
        <header className="postHeader postDetailHeader">
          <Link to={author ? `/profile/${author.id}` : "#"} className="postAuthor">
            <span className="igRing">
              <img
                className="igAvatar"
                width={32}
                height={32}
                src={getPublicImageUrl(author?.imagePath)}
                alt=""
                onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
              />
            </span>
            <span className="postAuthorName">{author?.name || "User"}</span>
          </Link>
          <button type="button" className="igIconButton postMore" onClick={() => setShowOptions(true)} aria-label="More options">
            <MoreHoriz />
          </button>
        </header>

        <div className="postDetailComments" ref={listRef}>
          {post.description && (
            <CommentRow user={author} text={post.description} createdAt={post.createdAt} />
          )}
          {comments === null ? (
            [0, 1, 2].map((i) => (
              <div key={i} className="commentRow">
                <div className="skeleton" style={{ width: 32, height: 32, borderRadius: "50%" }} />
                <div className="commentBody">
                  <div className="skeleton" style={{ width: `${70 - i * 15}%`, height: 12, marginBottom: 6 }} />
                  <div className="skeleton" style={{ width: 40, height: 10 }} />
                </div>
              </div>
            ))
          ) : comments.length === 0 && !post.description ? (
            <div className="postDetailEmpty igFadeIn">
              <h3>No comments yet.</h3>
              <p>Start the conversation.</p>
            </div>
          ) : (
            comments.map((c, i) => (
              <CommentRow
                key={c.id}
                user={c.user}
                text={c.text}
                createdAt={c.createdAt}
                className={c.isNew ? "commentNew" : "igFadeUp"}
                style={c.isNew ? undefined : { animationDelay: `${Math.min(i, 8) * 40}ms` }}
              />
            ))
          )}
        </div>

        <div className="postDetailFooter">
          <PostActions post={post} onLike={() => like()} onComment={() => inputRef.current?.focus()} />
          <time className="postDetailTime" dateTime={post.createdAt}>{timeAgo(post.createdAt)}</time>
          <form className="postAddComment postDetailForm" onSubmit={submitComment}>
            <input
              ref={inputRef}
              value={newComment}
              onChange={(e) => setNewComment(e.target.value)}
              placeholder="Add a comment…"
              aria-label="Add a comment"
              maxLength={500}
            />
            <button type="submit" className="igLinkButton" disabled={!newComment.trim() || isPosting}>
              Post
            </button>
          </form>
        </div>
      </div>

      {showOptions && (
        <PostOptions post={post} onClose={() => setShowOptions(false)} onDeleted={onDelete} />
      )}
    </div>
  );
}
