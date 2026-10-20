import { useRef } from "react";
import { toggleLike } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import { showToast } from "../toast/Toaster";

/**
 * Optimistic like toggle. `onlyLike` is used by double-tap, which never unlikes.
 * The backend publishes a PostLiked/PostUnliked event to Service Bus.
 */
export function usePostLike(post, onUpdate) {
  const pendingRef = useRef(false);

  return async function like({ onlyLike = false } = {}) {
    const { id: userId } = getCurrentUser();
    if (!userId || pendingRef.current) return;
    if (onlyLike && post.isLiked) return;

    const optimistic = {
      ...post,
      isLiked: !post.isLiked,
      likeCount: Math.max(0, post.likeCount + (post.isLiked ? -1 : 1)),
    };
    onUpdate(optimistic);
    pendingRef.current = true;

    try {
      const result = await toggleLike(post.id, userId);
      onUpdate({ ...optimistic, isLiked: result.isLiked, likeCount: result.likeCount });
    } catch {
      onUpdate(post);
      showToast("Couldn't update like. Try again.");
    } finally {
      pendingRef.current = false;
    }
  };
}
