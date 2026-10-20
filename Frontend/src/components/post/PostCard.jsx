import { useState } from "react";
import { Link } from "react-router-dom";
import { MoreHoriz } from "@mui/icons-material";
import PostMedia from "./PostMedia";
import PostActions from "./PostActions";
import PostOptions from "./PostOptions";
import PostModal from "./PostModal";
import { usePostLike } from "./usePostLike";
import { addComment } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { timeAgo } from "../../utils/time";
import { showToast } from "../toast/Toaster";
import "./post.css";

const captionPreviewLength = 125;

export default function PostCard({ post, onUpdate, onDelete, index = 0 }) {
  const [showDetail, setShowDetail] = useState(false);
  const [showOptions, setShowOptions] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);
  const [comment, setComment] = useState("");
  const [isPosting, setIsPosting] = useState(false);
  const like = usePostLike(post, onUpdate);
  const author = post.user;
  const authorLink = author ? `/profile/${author.id}` : "#";
  const caption = post.description || "";
  const isLongCaption = caption.length > captionPreviewLength;

  const submitComment = async (event) => {
    event.preventDefault();
    const text = comment.trim();
    const { id: userId } = getCurrentUser();
    if (!text || !userId || isPosting) return;
    setIsPosting(true);
    try {
      await addComment(post.id, userId, text);
      setComment("");
      onUpdate({ ...post, commentCount: post.commentCount + 1 });
      showToast("Comment posted");
    } catch {
      showToast("Couldn't post comment");
    } finally {
      setIsPosting(false);
    }
  };

  return (
    <article className="postCard igFadeUp" style={{ animationDelay: `${Math.min(index, 5) * 70}ms` }}>
      <header className="postHeader">
        <Link to={authorLink} className="postAuthor">
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
        <span className="postDot">•</span>
        <time className="postTime" dateTime={post.createdAt}>{timeAgo(post.createdAt)}</time>
        <button type="button" className="igIconButton postMore" onClick={() => setShowOptions(true)} aria-label="More options">
          <MoreHoriz />
        </button>
      </header>

      {post.imagePath ? (
        <PostMedia post={post} onDoubleTap={() => like({ onlyLike: true })} />
      ) : (
        <div className="postTextOnly">{caption}</div>
      )}

      <div className="postBody">
        <PostActions post={post} onLike={() => like()} onComment={() => setShowDetail(true)} />

        {post.imagePath && caption && (
          <p className="postCaption">
            <Link to={authorLink} className="postCaptionName">{author?.name}</Link>{" "}
            {isLongCaption && !isExpanded ? `${caption.slice(0, captionPreviewLength)}… ` : caption}
            {isLongCaption && !isExpanded && (
              <button type="button" className="postMoreText" onClick={() => setIsExpanded(true)}>more</button>
            )}
          </p>
        )}

        {post.commentCount > 0 && (
          <button type="button" className="postViewComments" onClick={() => setShowDetail(true)}>
            View {post.commentCount === 1 ? "1 comment" : `all ${post.commentCount} comments`}
          </button>
        )}

        <form className="postAddComment" onSubmit={submitComment}>
          <input
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder="Add a comment…"
            aria-label="Add a comment"
            maxLength={500}
          />
          {comment.trim() && (
            <button type="submit" className="igLinkButton igFadeIn" disabled={isPosting}>Post</button>
          )}
        </form>
      </div>

      {showDetail && (
        <PostModal post={post} onUpdate={onUpdate} onDelete={onDelete} onClose={() => setShowDetail(false)} />
      )}
      {showOptions && (
        <PostOptions post={post} onClose={() => setShowOptions(false)} onDeleted={onDelete} />
      )}
    </article>
  );
}
