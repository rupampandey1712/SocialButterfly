import "./topbar.css";
import {
  Search,
  Person,
  Chat,
  PersonAddRounded,
  CheckRounded,
  ChatBubbleOutlineRounded,
  HourglassEmptyRounded,
  CloseRounded,
} from "@mui/icons-material";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActive";
import { useState, useEffect, useRef, useCallback } from "react";
import axios from "axios";
import { Link, useNavigate } from "react-router-dom";
import { common } from "@mui/material/colors";
import AppLogo from "../logo/AppLogo";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { getApiUrl } from "../../utils/api";

export default function Topbar() {
  const navigate = useNavigate();
  const id = sessionStorage.getItem("Id");
  const currentUserId = id ? Number(id) : null;
  const image = getPublicImageUrl(sessionStorage.getItem("url") || sessionStorage.getItem("imagePath"));

  const [friendRequestsCount, setFriendRequestsCount] = useState(0);
  const [searchTerm, setSearchTerm] = useState("");
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [friendStatuses, setFriendStatuses] = useState({});
  const [submittingIds, setSubmittingIds] = useState({});

  const searchContainerRef = useRef(null);
  const debounceTimerRef = useRef(null);

  const logout = () => {
    sessionStorage.removeItem("tokens");
    sessionStorage.removeItem("isLoggedIn");
    sessionStorage.removeItem("Id");
    sessionStorage.removeItem("url");
    sessionStorage.removeItem("imagePath");
    window.location.reload();
    navigate("/");
  };

  const fetchFriendRequestsCount = useCallback(async () => {
    if (!currentUserId) {
      setFriendRequestsCount(0);
      return;
    }
    try {
      const response = await axios.get(getApiUrl(`/api/Friends/${currentUserId}/requests`));
      setFriendRequestsCount(response.data?.value ?? 0);
    } catch {
      setFriendRequestsCount(0);
    }
  }, [currentUserId]);

  useEffect(() => {
    fetchFriendRequestsCount();
    const interval = setInterval(fetchFriendRequestsCount, 15000);
    return () => clearInterval(interval);
  }, [fetchFriendRequestsCount]);

  const executeSearch = useCallback(async (query) => {
    const trimmed = query.trim();
    if (!trimmed) {
      setUsers([]);
      setHasSearched(false);
      setIsLoading(false);
      return;
    }

    try {
      setIsLoading(true);
      setHasSearched(true);
      const response = await axios.get(
        getApiUrl(`/api/Account/Search?Name=${encodeURIComponent(trimmed)}&Email=${encodeURIComponent(trimmed)}&Role=${encodeURIComponent(trimmed)}&Information=${encodeURIComponent(trimmed)}`)
      );

      const foundUsers = response.data || [];
      setUsers(foundUsers);
      setIsOpen(true);

      if (currentUserId && foundUsers.length > 0) {
        const statuses = {};
        await Promise.all(
          foundUsers.map(async (u) => {
            if (u.id === currentUserId) return;
            try {
              const statusRes = await axios.get(getApiUrl(`/api/Friends/status/${currentUserId}/${u.id}`));
              statuses[u.id] = statusRes.data?.status || "None";
            } catch {
              statuses[u.id] = "None";
            }
          })
        );
        setFriendStatuses(statuses);
      }
    } catch (error) {
      console.error("Error searching users:", error);
      setUsers([]);
    } finally {
      setIsLoading(false);
    }
  }, [currentUserId]);

  const handleInputChange = (event) => {
    const value = event.target.value;
    setSearchTerm(value);

    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    if (!value.trim()) {
      setUsers([]);
      setIsOpen(false);
      setHasSearched(false);
      return;
    }

    debounceTimerRef.current = setTimeout(() => {
      executeSearch(value);
    }, 350);
  };

  const handleSearchSubmit = (event) => {
    if (event) event.preventDefault();
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }
    executeSearch(searchTerm);
  };

  const handleSendFriendRequest = async (receiverId) => {
    if (!currentUserId || submittingIds[receiverId]) return;

    try {
      setSubmittingIds((prev) => ({ ...prev, [receiverId]: true }));
      await axios.post(getApiUrl("/api/Friends"), {
        senderId: currentUserId,
        receiverId: receiverId,
      });

      setFriendStatuses((prev) => ({
        ...prev,
        [receiverId]: "RequestSent",
      }));
    } catch (error) {
      console.error("Error sending friend request:", error);
    } finally {
      setSubmittingIds((prev) => ({ ...prev, [receiverId]: false }));
    }
  };

  const handleAcceptRequest = async (senderId) => {
    if (!currentUserId || submittingIds[senderId]) return;

    try {
      setSubmittingIds((prev) => ({ ...prev, [senderId]: true }));
      await axios.post(getApiUrl(`/api/Friends/${senderId}/${currentUserId}`));

      setFriendStatuses((prev) => ({
        ...prev,
        [senderId]: "Friends",
      }));
      fetchFriendRequestsCount();
    } catch (error) {
      console.error("Error accepting friend request:", error);
    } finally {
      setSubmittingIds((prev) => ({ ...prev, [senderId]: false }));
    }
  };

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (searchContainerRef.current && !searchContainerRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    };

    document.addEventListener("mousedown", handleOutsideClick);
    return () => {
      document.removeEventListener("mousedown", handleOutsideClick);
    };
  }, []);

  return (
    <div className="topbarContainer">
      <div className="topbarLeft">
        <Link to="/">
          <AppLogo compact />
        </Link>
      </div>

      <div className="topbarCenter" ref={searchContainerRef}>
        <form className="searchbar" onSubmit={handleSearchSubmit}>
          <Search className="searchIcon" onClick={handleSearchSubmit} style={{ cursor: "pointer" }} />
          <input
            placeholder="Search members by name, email or bio…"
            className="searchInput"
            value={searchTerm}
            onChange={handleInputChange}
            onFocus={() => {
              if (users.length > 0 || hasSearched) setIsOpen(true);
            }}
          />
          {searchTerm && (
            <button
              type="button"
              className="searchClearBtn"
              onClick={() => {
                setSearchTerm("");
                setUsers([]);
                setIsOpen(false);
                setHasSearched(false);
              }}
              aria-label="Clear search"
            >
              <CloseRounded fontSize="small" />
            </button>
          )}
        </form>

        {isOpen && (
          <div className="searchDropdown">
            <div className="searchDropdownHeader">
              <span>{isLoading ? "Searching members…" : `Search Results (${users.length})`}</span>
              <button
                type="button"
                className="searchDropdownClose"
                onClick={() => setIsOpen(false)}
              >
                Done
              </button>
            </div>

            <div className="searchDropdownBody">
              {isLoading ? (
                <div className="searchDropdownLoading">
                  <div className="searchDropdownSpinner"></div>
                  <p>Finding matching members…</p>
                </div>
              ) : users.length === 0 ? (
                <div className="searchDropdownEmpty">
                  <p>No members found matching &ldquo;{searchTerm}&rdquo;.</p>
                </div>
              ) : (
                <div className="searchResultsList">
                  {users.map((user) => {
                    const isSelf = user.id === currentUserId;
                    const status = friendStatuses[user.id] || "None";
                    const isSubmitting = submittingIds[user.id];

                    return (
                      <div key={user.id} className="searchResultItem">
                        <img
                          src={getPublicImageUrl(user.imagePath)}
                          className="searchResultAvatar"
                          alt={user.name || "Member"}
                          onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                        />
                        <div className="searchResultDetails">
                          <h4 className="searchResultName">
                            {user.name || "SocialButterfly Member"}
                            {isSelf && <span className="searchResultSelfTag">(You)</span>}
                          </h4>
                          <p className="searchResultEmail">{user.email}</p>
                          {user.information && (
                            <p className="searchResultBio">{user.information}</p>
                          )}
                        </div>

                        {!isSelf && (
                          <div className="searchResultActions">
                            {status === "Friends" ? (
                              <span className="searchResultFriendsBadge">
                                <CheckRounded fontSize="small" /> Friends
                              </span>
                            ) : status === "RequestSent" ? (
                              <span className="searchResultPendingBadge">
                                <HourglassEmptyRounded fontSize="small" /> Request Sent
                              </span>
                            ) : status === "RequestReceived" ? (
                              <button
                                type="button"
                                className="searchResultActionBtn btnAccept"
                                disabled={isSubmitting}
                                onClick={() => handleAcceptRequest(user.id)}
                              >
                                <CheckRounded fontSize="small" />
                                <span>{isSubmitting ? "Accepting…" : "Accept"}</span>
                              </button>
                            ) : (
                              <button
                                type="button"
                                className="searchResultActionBtn btnAddFriend"
                                disabled={isSubmitting}
                                onClick={() => handleSendFriendRequest(user.id)}
                              >
                                <PersonAddRounded fontSize="small" />
                                <span>{isSubmitting ? "Sending…" : "Add Friend"}</span>
                              </button>
                            )}

                            <Link
                              to={`/chat?userId=${user.id}`}
                              className="searchResultActionBtn btnMessage"
                              title={`Chat with ${user.name || "member"}`}
                              onClick={() => setIsOpen(false)}
                            >
                              <ChatBubbleOutlineRounded fontSize="small" />
                              <span>Chat</span>
                            </Link>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="topbarRight">
        <div className="topbarLinks">
          <Link to="/home">
            <span className="topbarLink">Homepage</span>
          </Link>
          <span className="topbarLink">Timeline</span>
          <button className="topbarLogoutBtn" onClick={logout}>
            Logout
          </button>
        </div>

        <div className="topbarIcons">
          <Link to="/requests" className="topbarIconItem" aria-label="Friend Requests">
            <Person />
            {friendRequestsCount > 0 && (
              <span className="topbarIconBadge">{friendRequestsCount}</span>
            )}
          </Link>
          <Link to="/chat" className="topbarIconItem" aria-label="Open chats">
            <Chat />
          </Link>
          <Link to="/requests" className="topbarIconItem" aria-label="Notifications">
            <NotificationsActiveIcon sx={{ color: common["white"] }} />
            {friendRequestsCount > 0 && (
              <span className="topbarIconBadge">{friendRequestsCount}</span>
            )}
          </Link>
        </div>

        <Link to="/">
          <img
            src={image}
            alt=""
            className="topbarImg"
            onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
          />
        </Link>
      </div>
    </div>
  );
}

