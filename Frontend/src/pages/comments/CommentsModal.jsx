// CommentsModal.jsx
import { useState, useEffect } from "react";
import axios from "axios";
import Modal from "react-bootstrap/Modal";
import { ModalBody, Form, FormControl, InputGroup, Button, Image } from "react-bootstrap";
import "./CommentsModal.css";

const CommentsModal = ({ show, postId, onClose }) => {
  const [comments, setComments] = useState([]);
  const [newComment, setNewComment] = useState("");

  useEffect(() => {
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
        console.error("Error submitting comment:", error);
      });
  };

  return (
    <Modal show={show} onHide={onClose} centered>
      <ModalBody>
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-2xl font-bold">Comments</h2>
          <span className="text-gray-600 cursor-pointer" onClick={onClose}>
            ×
          </span>
        </div>
        <div className="overflow-y-auto max-h-96">
          {comments.length > 0 ? (
            comments.map((comment) => (
              <div key={comment.id} className="flex items-start mb-4">
                <Image
                  width={40}
                  height={40}
                  className="mr-3 rounded-full"
                  src={comment.user.imagePath}
                />
                <div>
                  <h5 className="text-sm font-semibold">{comment.user.name}</h5>
                  <p className="text-gray-800 text-sm">{comment.text}</p>
                  <small className="text-gray-500 text-xs">
                    {new Date(comment.createdAt).toLocaleDateString()}
                  </small>
                </div>
              </div>
            ))
          ) : (
            <p className="text-gray-500 text-center">No comments yet.</p>
          )}
        </div>
        <Form onSubmit={handleSubmit} className="mt-4">
          <InputGroup>
            <Image
              width={32}
              height={32}
              className="mr-3 rounded-full"
              src={sessionStorage.getItem("imagePath")}
            />
            <FormControl
              placeholder="Add a comment..."
              value={newComment}
              onChange={(e) => setNewComment(e.target.value)}
              className="p-2 border-none focus:ring-0 focus:outline-none"
            />
            <Button type="submit" variant="outline-primary">Post</Button>
          </InputGroup>
        </Form>
      </ModalBody>
    </Modal>
  );
};

export default CommentsModal;
