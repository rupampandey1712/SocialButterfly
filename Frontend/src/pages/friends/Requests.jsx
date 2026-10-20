import React, { useState, useEffect, useCallback } from "react";
import "./Requests.css";
import axios from "axios";
import Topbar from "../../components/topbar/Topbar";
import { Link } from "react-router-dom";
import { CheckRounded, CloseRounded, PeopleRounded, ChatRounded } from "@mui/icons-material";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { getApiUrl } from "../../utils/api";

const MyFriends = () => {
  const currentUserId = Number(sessionStorage.getItem("Id"));
  const [requests, setRequests] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [actionMessage, setActionMessage] = useState("");
  const [processingId, setProcessingId] = useState(null);

  const fetchRequests = useCallback(async () => {
    if (!currentUserId) {
      setIsLoading(false);
      return;
    }
    try {
      setIsLoading(true);
      const response = await axios.get(getApiUrl("/api/Friends/me/requestuser"));
      setRequests(response.data?.value || []);
    } catch (error) {
      console.error("Error fetching friend requests:", error);
    } finally {
      setIsLoading(false);
    }
  }, [currentUserId]);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  const handleAccept = async (senderId, senderName) => {
    try {
      setProcessingId(senderId);
      await axios.post(getApiUrl(`/api/Friends/${senderId}`));
      setRequests((prev) => prev.filter((req) => req.id !== senderId));
      setActionMessage(`You are now friends with ${senderName || "user"}!`);
      setTimeout(() => setActionMessage(""), 4000);
    } catch (error) {
      console.error("Error accepting request:", error);
    } finally {
      setProcessingId(null);
    }
  };

  const handleDecline = async (senderId, senderName) => {
    try {
      setProcessingId(senderId);
      await axios.delete(getApiUrl(`/api/Friends/${senderId}/${currentUserId}`));
      setRequests((prev) => prev.filter((req) => req.id !== senderId));
      setActionMessage(`Declined request from ${senderName || "user"}.`);
      setTimeout(() => setActionMessage(""), 4000);
    } catch (error) {
      console.error("Error declining request:", error);
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="requestsPage">
      <Topbar />
      <div className="requestsContainer">
        <header className="requestsHeader">
          <div className="requestsHeaderTitle">
            <PeopleRounded className="requestsHeaderIcon" />
            <div>
              <h1>Friend Requests</h1>
              <p>Manage people who want to connect with you</p>
            </div>
          </div>
          {requests.length > 0 && (
            <span className="requestsBadge">{requests.length} Pending</span>
          )}
        </header>

        {actionMessage && (
          <div className="requestsAlert" role="status">
            {actionMessage}
          </div>
        )}

        {isLoading ? (
          <div className="requestsLoading">
            <div className="requestsSpinner"></div>
            <p>Loading friend requests…</p>
          </div>
        ) : requests.length === 0 ? (
          <div className="requestsEmpty">
            <PeopleRounded className="requestsEmptyIcon" />
            <h2>No pending requests</h2>
            <p>You have responded to all friend requests. Use the search bar to find new friends!</p>
            <Link to="/home" className="requestsBrowseBtn">
              Go to Feed
            </Link>
          </div>
        ) : (
          <div className="requestsGrid">
            {requests.map((friend) => (
              <div key={friend.id} className="modernRequestCard">
                <div className="requestCardAvatarWrapper">
                  <img
                    src={getPublicImageUrl(friend.imagePath)}
                    onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                    className="requestCardAvatar"
                    alt={friend.name || "Friend"}
                  />
                </div>
                <div className="requestCardContent">
                  <h3 className="requestCardName">{friend.name || "SocialButterfly Member"}</h3>
                  <p className="requestCardInfo">
                    {friend.information || friend.email || "Wants to be your friend"}
                  </p>
                  <div className="requestCardActions">
                    <button
                      className="requestBtnConfirm"
                      disabled={processingId === friend.id}
                      onClick={() => handleAccept(friend.id, friend.name)}
                    >
                      <CheckRounded fontSize="small" />
                      <span>{processingId === friend.id ? "Adding..." : "Confirm"}</span>
                    </button>
                    <button
                      className="requestBtnDelete"
                      disabled={processingId === friend.id}
                      onClick={() => handleDecline(friend.id, friend.name)}
                    >
                      <CloseRounded fontSize="small" />
                      <span>Decline</span>
                    </button>
                    <Link
                      to={`/chat?userId=${friend.id}`}
                      className="requestBtnMessage"
                      title="Send message"
                    >
                      <ChatRounded fontSize="small" />
                    </Link>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default MyFriends;

