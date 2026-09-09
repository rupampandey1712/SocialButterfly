import { useState, useEffect } from "react";
import axios from "axios";
import Modal from "react-bootstrap/Modal";
import { ModalBody } from "react-bootstrap";
import { Media } from "reactstrap";
import { Image } from "react-bootstrap";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import "./CommentsModal.css";

const CommentModal = ({ show, postId, onClose }) => {
  const [comments, setComments] = useState([]);
  const [newComment, setNewComment] = useState("");

  useEffect(() => {
    if (!postId) return;
    axios
      .get(`https://localhost:7151/api/Comment/${postId}`)
      .then((response) => {
        setComments(response.data);
      })
      .catch((error) => {
        console.error("Error fetching comments:", error);
      });
  }, [postId]);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!newComment.trim()) return;

    axios
      .post(`https://localhost:7151/api/Comment`, {
        text: newComment,
        postId: postId,
        userId: sessionStorage.getItem("Id"),
      })
      .then((response) => {
        setComments((existingComments) => [...existingComments, response.data]);
        setNewComment("");
      })
      .catch((error) => {
        console.error("Error creating comment:", error);
      });
  };

  return (
    <Modal show={show} onHide={onClose}>
      <ModalBody>
        <span className="close" onClick={onClose}>
          ×
        </span>
        <h2>Comments</h2>
        {comments.length > 0 ? (
          comments.map((comment) => (
            <Media key={comment.id} className="d-flex align-items-start mb-3">
              <Image
                width={48}
                height={48}
                className="mr-3 rounded-circle"
                src={getPublicImageUrl(comment.user?.imagePath)}
                onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                roundedCircle
              />
              <div className="media-body">
                <small className="text-muted">{new Date(comment.createdAt).toLocaleDateString()}</small>
                <h5>{comment.user?.name}</h5>
                <p>{comment.text}</p>
              </div>
            </Media>
          ))
        ) : (
          <p>No comments yet.</p>
        )}
        <form onSubmit={handleSubmit} className="mt-3">
          <label htmlFor="newComment" className="form-label">Add a comment:</label>
          <textarea
            id="newComment"
            className="form-control mb-2"
            value={newComment}
            onChange={(e) => setNewComment(e.target.value)}
          />
          <button type="submit" className="btn btn-primary">Submit</button>
        </form>
      </ModalBody>
    </Modal>
  );
};

export default CommentModal;
