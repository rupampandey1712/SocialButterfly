import { useState } from "react";
import { ChatBubbleOutlineRounded, Favorite, FavoriteBorder, SendOutlined } from "@mui/icons-material";
import { formatCount } from "../../utils/time";
import { showToast } from "../toast/Toaster";

export function copyPostLink(postId) {
  const url = `${window.location.origin}/p/${postId}`;
  if (!navigator.clipboard) {
    showToast(url);
    return;
  }
  navigator.clipboard.writeText(url).then(
    () => showToast("Link copied to clipboard"),
    () => showToast(url)
  );
}

export default function PostActions({ post, onLike, onComment }) {
  const [popKey, setPopKey] = useState(0);

  const handleLike = () => {
    setPopKey((key) => key + 1);
    onLike();
  };

  return (
    <>
      <div className="postActions">
        <button
          type="button"
          className={`igIconButton postLikeButton ${post.isLiked ? "isLiked" : ""}`}
          onClick={handleLike}
          aria-label={post.isLiked ? "Unlike" : "Like"}
          aria-pressed={post.isLiked}
        >
          <span key={popKey} className={popKey ? "heartPop" : ""}>
            {post.isLiked ? <Favorite /> : <FavoriteBorder />}
          </span>
        </button>
        <button type="button" className="igIconButton" onClick={onComment} aria-label="Comment">
          <ChatBubbleOutlineRounded style={{ transform: "scaleX(-1)" }} />
        </button>
        <button type="button" className="igIconButton" onClick={() => copyPostLink(post.id)} aria-label="Share">
          <SendOutlined style={{ transform: "rotate(-24deg) translateY(-2px)" }} />
        </button>
      </div>
      <div className="postLikes">
        {post.likeCount > 0 ? (
          <span key={post.likeCount} className="countBump">
            {formatCount(post.likeCount)} {post.likeCount === 1 ? "like" : "likes"}
          </span>
        ) : (
          <span className="postLikesEmpty">Be the first to <b>like this</b></span>
        )}
      </div>
    </>
  );
}
