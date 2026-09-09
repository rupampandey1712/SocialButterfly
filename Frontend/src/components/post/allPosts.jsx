import "./post.css";
import { MoreVert } from "@mui/icons-material";
import { useState, useEffect } from "react";
import axios from "axios";
import CommentsModal from "../../pages/comments/CommentsModal";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
 
export default function AllPost() {
  const [showCommentModal, setShowCommentModal] = useState(false);
  const [currentPostId, setCurrentPostId] = useState(null);
  const id = sessionStorage.getItem("Id");
  const [data, setData] = useState([]);

  useEffect(() => {
    axios
      .get(`https://localhost:7151/api/Post/UserPost`)
      .then((response) => {
        setData(response.data || []);
      })
      .catch((error) => {
        console.error("Error fetching posts:", error);
        setData([]);
      });
  }, [id]);

  const likeHandler = async (postId, initialLikeCount, initialIsLiked) => {
    try {
      const response = await axios.post(
        `https://localhost:7151/api/Likes?postId=${postId}&userId=${id}`
      );
      const updatedLikeCount = response.data;
      setData((prevData) =>
        prevData.map((item) =>
          item.id === postId
            ? {
                ...item,
                likeCount: updatedLikeCount,
                isLiked: !initialIsLiked,
              }
            : item
        )
      );
    } catch (error) {
      console.error("Error liking post:", error);
    }
  };

  const handleCommentButtonClick = (postId) => {
    setCurrentPostId(postId);
    setShowCommentModal(true);
  };

  const handleCloseCommentModal = () => {
    setShowCommentModal(false);
    setCurrentPostId(null);
  };

  return (
    <>
      {data.length === 0 && (
        <div className="post emptyFeed">
          <div className="postWrapper">
            <div className="emptyFeedIcon">SB</div>
            <h3>No posts yet</h3>
            <p>Once your community starts sharing, posts will appear here in a clean, organized feed.</p>
          </div>
        </div>
      )}
      {data.map((post) => (
        <div className="post" key={post.id}>
          <div className="postWrapper">
            <div className="postTop">
              <div className="postTopLeft">
                <img
                  className="postProfileImg"
                  src={getPublicImageUrl(post.user?.imagePath)}
                  alt=""
                  onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                />
                <span className="postUsername">{post.user?.name || "User"}</span>
                <span className="postDate">
                  {post.createdAt ? new Date(post.createdAt).toLocaleDateString() : ""}
                </span>
              </div>
              <div className="postTopRight">
                <MoreVert />
              </div>
            </div>
            <div className="postCenter">
              <span className="postText">{post.descriprion}</span>
              {post.imagePath &&
              (post.imagePath.endsWith(".jpg") ||
                post.imagePath.endsWith(".png") ||
                post.imagePath.endsWith(".jpeg")) ? (
                <img className="postImg" src={getPublicImageUrl(post.imagePath)} alt="" />
              ) : post.imagePath &&
                (post.imagePath.endsWith(".mp4") || post.imagePath.endsWith(".mov")) ? (
                <video className="postImg" controls>
                  <source src={getPublicImageUrl(post.imagePath)} type="video/mp4" />
                  Your browser does not support the video tag.
                </video>
              ) : post.imagePath &&
                (post.imagePath.endsWith(".mp3") || post.imagePath.endsWith(".wav")) ? (
                <audio className="postImg" controls>
                  <source src={getPublicImageUrl(post.imagePath)} type="audio/mpeg" />
                  Your browser does not support the audio tag.
                </audio>
              ) : null}
            </div>
            <div className="postBottom">
              <div className="postBottomLeft">
                <span
                  className="likeIcon"
                  style={{ color: post.isLiked ? "red" : "gray", cursor: "pointer" }}
                  onClick={() => likeHandler(post.id, post.likeCount, post.isLiked)}
                >
                  &#10084;
                </span>
                <span className="postLikeCounter">{post.likeCount} people like it</span>
              </div>
              <div className="postBottomRight">
                <button
                  className="btn btn-dark"
                  onClick={() => handleCommentButtonClick(post.id)}
                >
                  comments
                </button>
              </div>
            </div>
          </div>
          {showCommentModal && currentPostId === post.id && (
            <CommentsModal
              show={showCommentModal}
              postId={currentPostId}
              onClose={handleCloseCommentModal}
            />
          )}
        </div>
      ))}
    </>
  );
}
