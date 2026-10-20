import { useNavigate } from "react-router-dom";
import AnimatedModal from "../modal/AnimatedModal";
import { copyPostLink } from "./PostActions";
import { deletePost } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import { showToast } from "../toast/Toaster";

export default function PostOptions({ post, onClose, onDeleted }) {
  const navigate = useNavigate();
  const { id: userId } = getCurrentUser();
  const isOwner = post.user?.id === userId;

  return (
    <AnimatedModal onClose={onClose} showClose={false} className="postOptions" label="Post options">
      {(close) => (
        <>
          {isOwner && (
            <button
              type="button"
              className="postOption danger"
              onClick={async () => {
                try {
                  await deletePost(post.id, userId);
                  showToast("Post deleted");
                  close();
                  onDeleted?.(post.id);
                } catch {
                  showToast("Couldn't delete this post");
                  close();
                }
              }}
            >
              Delete
            </button>
          )}
          <button type="button" className="postOption" onClick={() => { close(); navigate(`/p/${post.id}`); }}>
            Go to post
          </button>
          {post.user && !isOwner && (
            <button type="button" className="postOption" onClick={() => { close(); navigate(`/profile/${post.user.id}`); }}>
              About this account
            </button>
          )}
          <button type="button" className="postOption" onClick={() => { copyPostLink(post.id); close(); }}>
            Copy link
          </button>
          <button type="button" className="postOption" onClick={close}>
            Cancel
          </button>
        </>
      )}
    </AnimatedModal>
  );
}
