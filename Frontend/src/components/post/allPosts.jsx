import { useCallback, useEffect, useRef, useState } from "react";
import { CheckCircleOutlineRounded } from "@mui/icons-material";
import PostCard from "./PostCard";
import { fetchFeed, POST_CREATED_EVENT } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import "./post.css";

const pageSize = 8;

export function PostSkeleton() {
  return (
    <div className="postCard postSkeleton" aria-hidden="true">
      <div className="postHeader">
        <div className="skeleton" />
        <div className="skeleton" style={{ width: 140, height: 12 }} />
      </div>
      <div className="skeleton postSkeletonMedia" />
      <div className="skeleton" style={{ width: 90, height: 12, margin: "14px 4px 8px" }} />
      <div className="skeleton" style={{ width: "70%", height: 12, margin: "0 4px" }} />
    </div>
  );
}

export default function AllPost() {
  const { id: viewerId } = getCurrentUser();
  const [posts, setPosts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasMore, setHasMore] = useState(true);
  const [error, setError] = useState(false);
  const sentinelRef = useRef(null);
  const loadingRef = useRef(false);
  const postCountRef = useRef(0);
  postCountRef.current = posts.length;

  const loadMore = useCallback(async () => {
    if (loadingRef.current) return;
    loadingRef.current = true;
    setIsLoading(true);
    try {
      const page = await fetchFeed(viewerId, postCountRef.current, pageSize);
      setPosts((prev) => {
        const seen = new Set(prev.map((p) => p.id));
        return [...prev, ...page.filter((p) => !seen.has(p.id))];
      });
      setHasMore(page.length === pageSize);
      setError(false);
    } catch {
      setError(true);
    } finally {
      loadingRef.current = false;
      setIsLoading(false);
    }
  }, [viewerId]);

  useEffect(() => {
    loadMore();
  }, [loadMore]);

  // Infinite scroll: load the next page as the sentinel approaches the viewport.
  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || !hasMore || error) return undefined;
    const observer = new IntersectionObserver(
      (entries) => entries[0].isIntersecting && loadMore(),
      { rootMargin: "600px 0px" }
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [hasMore, error, loadMore, posts.length]);

  // A post created from the nav's Create modal appears at the top immediately.
  useEffect(() => {
    const onCreated = (event) => {
      const created = event.detail;
      if (!created?.id) return;
      const { id, name, image } = getCurrentUser();
      setPosts((prev) => [
        {
          id: created.id,
          imagePath: created.imagePath,
          description: created.descriprion,
          createdAt: created.createdAt || new Date().toISOString(),
          user: { id, name, imagePath: image },
          likeCount: 0,
          commentCount: 0,
          isLiked: false,
        },
        ...prev,
      ]);
      window.scrollTo({ top: 0, behavior: "smooth" });
    };
    window.addEventListener(POST_CREATED_EVENT, onCreated);
    return () => window.removeEventListener(POST_CREATED_EVENT, onCreated);
  }, []);

  const updatePost = useCallback((updated) => {
    setPosts((prev) => prev.map((p) => (p.id === updated.id ? updated : p)));
  }, []);

  const removePost = useCallback((postId) => {
    setPosts((prev) => prev.filter((p) => p.id !== postId));
  }, []);

  if (!isLoading && !error && posts.length === 0) {
    return (
      <div className="feedEmpty igFadeUp">
        <div className="feedEmptyIcon">
          <CheckCircleOutlineRounded />
        </div>
        <h3>No posts yet</h3>
        <p>When people share photos and videos, they'll appear here. Tap Create to share the first one.</p>
      </div>
    );
  }

  return (
    <>
      {posts.map((post, index) => (
        <PostCard
          key={post.id}
          post={post}
          index={index % pageSize}
          onUpdate={updatePost}
          onDelete={removePost}
        />
      ))}

      {isLoading && (
        <>
          <PostSkeleton />
          {posts.length === 0 && <PostSkeleton />}
        </>
      )}

      {error && (
        <div className="feedEnd">
          <p>Couldn't load posts.</p>
          <button type="button" className="igButton" onClick={loadMore}>Retry</button>
        </div>
      )}

      {!hasMore && posts.length > 0 && (
        <div className="feedEnd igFadeIn">
          <div className="feedEndCheck"><CheckCircleOutlineRounded /></div>
          <h4>You're all caught up</h4>
          <p>You've seen all new posts.</p>
        </div>
      )}

      <div ref={sentinelRef} aria-hidden="true" />
    </>
  );
}
