import "./post.css";
import { MoreVert } from "@mui/icons-material";
import { useState } from "react";
import axios from "axios";
import { useEffect } from "react";
import CommentsModal from "../../pages/comments/CommentsModal";
 
export default function AllPost() {

  const [showCommentModal, setShowCommentModal] = useState(false);
  const [currentPostId, setCurrentPostId] = useState(null);
  const id = sessionStorage.getItem("Id");
  const [data, setData] = useState([]);
  
 
  useEffect(() => {
    axios.get(`https://localhost:7151/api/Post/UserPost`).then((response) => {
      setData((existData) => {
        console.log(response.data);
        return response.data;
      });
    }).catch((error) => {
      console.error("Error fetching posts:", error);
      setData([]);
    });
  }, [id]);
 
  const likeHandler = async (postId, initialLikeCount, initialIsLiked) => {
    try {
      const response = await axios.post(`https://localhost:7151/api/Likes?postId=${postId}&userId=${id}`);
      const updatedLikeCount = response.data;
      setData((prevData) =>
        prevData.map((data) =>
          data.id === postId
            ? {
                ...data,
                likeCount: updatedLikeCount,
                isLiked: !initialIsLiked,
              }
            : data
        )
      );
    } catch (error) {
      console.error("Error liking post:", error);
    }
  };
 
  const handleCommentButtonClick = (postId) => {
    setCurrentPostId(postId);
    console.log(currentPostId);
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
      {data.map((data) => (
<div className="post" key={data.id}>
<div className="postWrapper">
<div className="postTop">
<div className="postTopLeft">
<img
                  className="postProfileImg"
                  src={data.user.imagePath}
                  alt=""
                />
<span className="postUsername">{data.user.name}</span>
<span className="postDate">
                  {new Date(data.createdAt).toLocaleDateString()}
</span>
</div>
<div className="postTopRight">
<MoreVert />
</div>
</div>
<div className="postCenter">
<span className="postText">{data.descriprion}</span>
              {data.imagePath.endsWith(".jpg") ||
              data.imagePath.endsWith(".png") ? (
<img className="postImg" src={data.imagePath} alt="" />
              ) : data.imagePath.endsWith(".mp4") ||
                data.imagePath.endsWith(".mov") ? (
<video className="postImg" controls>
<source src={data.imagePath} type="video/mp4" />
                  Your browser does not support the video tag.
</video>
              ) : data.imagePath.endsWith(".mp3") ||
                data.imagePath.endsWith(".wav") ? (
<audio className="postImg" controls>
<source src={data.imagePath} type="audio/mpeg" />
                  Your browser does not support the audio tag.
</audio>
              ) : null}
</div>
<div className="postBottom">
<div className="postBottomLeft">
<span
                className="likeIcon"
                style={{ color: data.isLiked ? "red" : "gray" }}
                onClick={() => likeHandler(data.id, data.likeCount, data.isLiked)}
>
&#10084;
</span>
<span className="postLikeCounter">{data.likeCount} people like it</span>
</div>
<div className="postBottomRight">
<button
                  className="btn btn-dark"
                  onClick={() => handleCommentButtonClick(data.id)}
>
                  comments
</button>
</div>
</div>
</div>
          {showCommentModal && currentPostId && (
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
