import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import Topbar from "../../components/topbar/Topbar";
import PostDetail from "../../components/post/PostDetail";
import { fetchPost } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import "./postPage.css";

/** Standalone post view behind shared /p/:postId links and notification clicks. */
export default function PostPage() {
  const { postId } = useParams();
  const navigate = useNavigate();
  const { id: viewerId } = getCurrentUser();
  const [post, setPost] = useState(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    setPost(null);
    setNotFound(false);
    fetchPost(postId, viewerId)
      .then(setPost)
      .catch(() => setNotFound(true));
  }, [postId, viewerId]);

  return (
    <>
      <Topbar />
      <div className="postPage">
        {notFound ? (
          <div className="postPageMissing igFadeIn">
            <h2>Sorry, this post isn't available.</h2>
            <p>The post may have been deleted. <Link to="/">Go back to SocialButterfly.</Link></p>
          </div>
        ) : post ? (
          <div className="postPageCard igFadeUp">
            <PostDetail post={post} onUpdate={setPost} onDelete={() => navigate("/profile")} />
          </div>
        ) : (
          <div className="postPageCard skeleton postPageSkeleton" />
        )}
      </div>
    </>
  );
}
