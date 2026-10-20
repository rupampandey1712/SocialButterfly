import AnimatedModal from "../modal/AnimatedModal";
import PostDetail from "./PostDetail";

export default function PostModal({ post, onUpdate, onDelete, onClose }) {
  return (
    <AnimatedModal onClose={onClose} className="postModalPanel" label="Post">
      {(close) => (
        <PostDetail
          post={post}
          onUpdate={onUpdate}
          onDelete={(postId) => {
            close();
            onDelete?.(postId);
          }}
        />
      )}
    </AnimatedModal>
  );
}
