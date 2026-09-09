import "./post.css";
import { MoreVert } from "@mui/icons-material";
import { useState } from "react";
import axios from "axios";
import CommentModal from "../../pages/comments/Comments";
import { useEffect } from "react";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
// import Loader from "../loader/Loader";

export default function Post() {
  const [like,setLike] = useState(1)
  const [isLiked,setIsLiked] = useState(false)
  const [showCommentModal, setShowCommentModal] = useState(false);
  const [currentPostId,setCurrentPostId] = useState(null)
  const id=sessionStorage.getItem('Id')
  // const [isLoading, setIsLoading] = useState(true)
  const [data,setData] = useState([])

  useEffect(()=>{
    if (!id) {
      setData([]);
      return;
    }
    axios.get(`https://localhost:7151/api/Post/${id}`).then((response)=>{
      console.log(response);
      setData((existData)=>{
            return response.data;
            });
        }).catch((error) => {
          console.error("Error fetching profile posts:", error);
          setData([]);
        });
    },[id]);

  const likeHandler =()=>{
    setLike(isLiked ? like-1 : like+1)
    setIsLiked(!isLiked)
  }


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
{
  data.length === 0 && (
    <div className="post emptyFeed">
      <div className="postWrapper">
        <div className="emptyFeedIcon">SB</div>
        <h3>No posts yet</h3>
        <p>Your profile posts will appear here after you share something.</p>
      </div>
    </div>
  )
}
{
  data.map((data)=>(
    <div className="post" key={data.id}>

<div className="postWrapper" >
        <div className="postTop">
          <div className="postTopLeft">
            <img
              className="postProfileImg"
              src={getPublicImageUrl(data.user?.imagePath)}
              alt=""
              onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
            />
            <span className="postUsername">
              {data.user?.name || "User"}
            </span>
            <span className="postDate">{data.createdAt ? new Date(data.createdAt).toLocaleDateString() : ""}</span>
          </div>
          <div className="postTopRight">
            <MoreVert />
          </div>
        </div>
        <div className="postCenter">
          <span className="postText">{data.descriprion}</span>
          {data.imagePath && (data.imagePath.endsWith('.jpg') || data.imagePath.endsWith('.png') || data.imagePath.endsWith('.jpeg')) ? (
            <img className="postImg" src={getPublicImageUrl(data.imagePath)} alt="" />
          ) : data.imagePath && (data.imagePath.endsWith('.mp4') || data.imagePath.endsWith('.mov')) ? (
            <video className="postImg" controls>
              <source src={getPublicImageUrl(data.imagePath)} type="video/mp4" />
              Your browser does not support the video tag.
            </video>
          ) : data.imagePath && (data.imagePath.endsWith('.mp3') || data.imagePath.endsWith('.wav')) ? (
            <audio className="postImg" controls>
              <source src={getPublicImageUrl(data.imagePath)} type="audio/mpeg" />
              Your browser does not support the audio tag.
            </audio>
          ) : null}
        </div>
        <div className="postBottom">
          <div className="postBottomLeft">
            <img className="likeIcon" src="assets/like.png" onClick={likeHandler} alt="" />
            <img className="likeIcon" src="assets/heart.png" onClick={likeHandler} alt="" />
            <span className="postLikeCounter">{like} people like it</span>
          </div>
          <div className="postBottomRight">
          <button
                  className="btn btn-dark"
                  onClick={()=>handleCommentButtonClick(data.id)}
                >
                  comments
                </button>
              </div>
            </div>
          </div>

          {showCommentModal && currentPostId && (
        <CommentModal
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
