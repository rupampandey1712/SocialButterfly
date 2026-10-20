import "./topbar.css";
import {
  Home as HomeFilled,
  HomeOutlined,
  Search,
  SearchRounded,
  SendOutlined,
  Send,
  FavoriteBorder,
  Favorite,
  AddBoxOutlined,
  MenuRounded,
  PersonAddRounded,
  CheckRounded,
  HourglassEmptyRounded,
  CloseRounded,
  LogoutRounded,
  PeopleAltOutlined,
  Explore as ExploreFilled,
  ExploreOutlined,
  SettingsOutlined,
  DarkModeOutlined,
  LightModeOutlined,
  GridOnRounded,
  AutoAwesomeRounded,
} from "@mui/icons-material";
import { useState, useEffect, useRef, useCallback } from "react";
import axios from "axios";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import AppLogo from "../logo/AppLogo";
import CreatePostModal from "../createPost/CreatePostModal";
import StoryComposer from "../stories/StoryComposer";
import AnimatedModal from "../modal/AnimatedModal";
import { applyTheme } from "../../utils/theme";
import { updateSettings } from "../../utils/account";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { getApiUrl } from "../../utils/api";
import { getCurrentUser, logout } from "../../utils/session";
import { timeAgo } from "../../utils/time";
import {
  acceptFriendRequest,
  fetchNotifications,
  fetchUnreadNotificationCount,
  markNotificationsRead,
  sendFriendRequest,
} from "../../utils/posts";

const pollIntervalMs = 15000;

function Avatar({ src, size = 24, className = "" }) {
  return (
    <img
      className={`igAvatar ${className}`}
      width={size}
      height={size}
      src={src}
      alt=""
      onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
    />
  );
}

function notificationMessage(n) {
  switch (n.type) {
    case "PostLiked": return "liked your post.";
    case "PostCommented": return n.text ? `commented: ${n.text}` : "commented on your post.";
    case "PostCreated": return "shared a new post.";
    case "FriendRequestSent": return "sent you a friend request.";
    case "FriendRequestAccepted": return "accepted your friend request.";
    case "UserFollowed": return "started following you.";
    default: return "interacted with you.";
  }
}

function SearchPanel({ currentUserId, onNavigate }) {
  const [searchTerm, setSearchTerm] = useState("");
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [friendStatuses, setFriendStatuses] = useState({});
  const [submittingIds, setSubmittingIds] = useState({});
  const debounceTimerRef = useRef(null);
  const inputRef = useRef(null);

  useEffect(() => {
    const timer = setTimeout(() => inputRef.current?.focus(), 250);
    return () => clearTimeout(timer);
  }, []);

  const executeSearch = useCallback(async (query) => {
    const trimmed = query.trim();
    if (!trimmed) return;
    try {
      setIsLoading(true);
      setHasSearched(true);
      const q = encodeURIComponent(trimmed);
      const response = await axios.get(getApiUrl(`/api/Account/Search?Name=${q}&Email=${q}&Role=${q}&Information=${q}`));
      const found = response.data || [];
      setUsers(found);

      if (currentUserId) {
        const statuses = {};
        await Promise.all(
          found.filter((u) => u.id !== currentUserId).map(async (u) => {
            try {
              const res = await axios.get(getApiUrl(`/api/Friends/status/${u.id}`));
              statuses[u.id] = res.data?.status || "None";
            } catch {
              statuses[u.id] = "None";
            }
          })
        );
        setFriendStatuses(statuses);
      }
    } catch {
      setUsers([]);
    } finally {
      setIsLoading(false);
    }
  }, [currentUserId]);

  const handleChange = (event) => {
    const value = event.target.value;
    setSearchTerm(value);
    clearTimeout(debounceTimerRef.current);
    if (!value.trim()) {
      setUsers([]);
      setHasSearched(false);
      return;
    }
    debounceTimerRef.current = setTimeout(() => executeSearch(value), 350);
  };

  const runAction = async (userId, action, nextStatus) => {
    if (submittingIds[userId]) return;
    setSubmittingIds((prev) => ({ ...prev, [userId]: true }));
    try {
      await action();
      setFriendStatuses((prev) => ({ ...prev, [userId]: nextStatus }));
    } catch (error) {
      console.error("Friend action failed:", error);
    } finally {
      setSubmittingIds((prev) => ({ ...prev, [userId]: false }));
    }
  };

  return (
    <>
      <h2 className="navPanelTitle">Search</h2>
      <form className="navSearchBox" onSubmit={(e) => { e.preventDefault(); executeSearch(searchTerm); }}>
        <SearchRounded className="navSearchIcon" />
        <input
          ref={inputRef}
          placeholder="Search"
          value={searchTerm}
          onChange={handleChange}
          aria-label="Search members"
        />
        {searchTerm && (
          <button
            type="button"
            className="navSearchClear"
            onClick={() => { setSearchTerm(""); setUsers([]); setHasSearched(false); }}
            aria-label="Clear search"
          >
            <CloseRounded fontSize="small" />
          </button>
        )}
      </form>

      <div className="navPanelBody">
        {isLoading ? (
          <div className="igSpinner" />
        ) : !hasSearched ? (
          <p className="navPanelHint">Search members by name, email or bio.</p>
        ) : users.length === 0 ? (
          <p className="navPanelHint">No results found.</p>
        ) : (
          users.map((user, i) => {
            const isSelf = user.id === currentUserId;
            const status = friendStatuses[user.id] || "None";
            const isSubmitting = submittingIds[user.id];
            return (
              <div key={user.id} className="navUserRow igFadeUp" style={{ animationDelay: `${Math.min(i, 8) * 35}ms` }}>
                <Link to={`/profile/${user.id}`} className="navUserLink" onClick={onNavigate}>
                  <Avatar src={getPublicImageUrl(user.imagePath)} size={44} />
                  <span className="navUserText">
                    <strong>{user.username || user.name || "Member"}{isSelf && " (you)"}</strong>
                    <span>{user.name}{user.information ? ` · ${user.information}` : ""}</span>
                  </span>
                </Link>
                {!isSelf && (
                  status === "Friends" ? (
                    <span className="navUserBadge"><CheckRounded fontSize="small" /> Friends</span>
                  ) : status === "RequestSent" ? (
                    <span className="navUserBadge"><HourglassEmptyRounded fontSize="small" /> Requested</span>
                  ) : status === "RequestReceived" ? (
                    <button
                      type="button"
                      className="igButton navUserAction"
                      disabled={isSubmitting}
                      onClick={() => runAction(user.id, () => acceptFriendRequest(user.id, currentUserId), "Friends")}
                    >
                      Confirm
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="igButton navUserAction"
                      disabled={isSubmitting}
                      onClick={() => runAction(user.id, () => sendFriendRequest(currentUserId, user.id), "RequestSent")}
                    >
                      <PersonAddRounded fontSize="small" /> Follow
                    </button>
                  )
                )}
              </div>
            );
          })
        )}
      </div>
    </>
  );
}

function NotificationsPanel({ currentUserId, requestCount, onNavigate, onRead }) {
  const [notifications, setNotifications] = useState(null);

  useEffect(() => {
    if (!currentUserId) return;
    let isActive = true;
    fetchNotifications(currentUserId)
      .then((data) => {
        if (!isActive) return;
        setNotifications(data);
        if (data.some((n) => !n.isRead)) {
          markNotificationsRead(currentUserId).then(onRead).catch(() => {});
        }
      })
      .catch(() => isActive && setNotifications([]));
    return () => { isActive = false; };
  }, [currentUserId, onRead]);

  const unread = notifications?.filter((n) => !n.isRead) ?? [];
  const earlier = notifications?.filter((n) => n.isRead) ?? [];

  const renderRow = (n, i) => (
    <Link
      key={n.id}
      to={n.postId ? `/p/${n.postId}` : `/profile/${n.actor?.id}`}
      className={`navNotification igFadeUp ${n.isRead ? "" : "isUnread"}`}
      style={{ animationDelay: `${Math.min(i, 10) * 35}ms` }}
      onClick={onNavigate}
    >
      <Avatar src={getPublicImageUrl(n.actor?.imagePath)} size={44} />
      <p>
        <strong>{n.actor?.name || "Someone"}</strong> {notificationMessage(n)}{" "}
        <time dateTime={n.createdAt}>{timeAgo(n.createdAt)}</time>
      </p>
      {n.postImagePath && /\.(jpe?g|png|gif|webp)(\?|$)/i.test(n.postImagePath) && (
        <img className="navNotificationThumb" src={getPublicImageUrl(n.postImagePath, "")} alt="" />
      )}
    </Link>
  );

  return (
    <>
      <h2 className="navPanelTitle">Notifications</h2>
      <div className="navPanelBody">
        {requestCount > 0 && (
          <Link to="/requests" className="navRequestsRow igFadeUp" onClick={onNavigate}>
            <span className="navRequestsIcon"><PeopleAltOutlined /></span>
            <span className="navUserText">
              <strong>Friend requests</strong>
              <span>{requestCount} pending</span>
            </span>
            <span className="navRequestsDot" />
          </Link>
        )}

        {notifications === null ? (
          <div className="igSpinner" />
        ) : notifications.length === 0 ? (
          <div className="navPanelEmpty igFadeIn">
            <FavoriteBorder />
            <h3>Activity on your posts</h3>
            <p>When someone likes or comments on your posts, you'll see it here.</p>
          </div>
        ) : (
          <>
            {unread.length > 0 && <h4 className="navPanelSection">New</h4>}
            {unread.map(renderRow)}
            {earlier.length > 0 && <h4 className="navPanelSection">Earlier</h4>}
            {earlier.map(renderRow)}
          </>
        )}
      </div>
    </>
  );
}

export default function Topbar() {
  const location = useLocation();
  const navigate = useNavigate();
  const user = getCurrentUser();
  const [panel, setPanel] = useState(null);
  const [showCreate, setShowCreate] = useState(false);
  const [showMore, setShowMore] = useState(false);
  const [requestCount, setRequestCount] = useState(0);
  const [unreadCount, setUnreadCount] = useState(0);
  const panelRef = useRef(null);

  // Every page that renders the nav gets the body offset for the fixed rail.
  useEffect(() => {
    document.body.classList.add("hasAppNav");
    return () => document.body.classList.remove("hasAppNav");
  }, []);

  const refreshCounts = useCallback(async () => {
    if (!user.id) return;
    try {
      const [requests, unread] = await Promise.all([
        axios.get(getApiUrl("/api/Friends/me/requests")).then((r) => r.data?.value ?? 0),
        fetchUnreadNotificationCount(user.id),
      ]);
      setRequestCount(requests);
      setUnreadCount(unread);
    } catch {
      // Counts are decorative; keep the last known values.
    }
  }, [user.id]);

  useEffect(() => {
    refreshCounts();
    const interval = setInterval(refreshCounts, pollIntervalMs);
    return () => clearInterval(interval);
  }, [refreshCounts]);

  useEffect(() => {
    setPanel(null);
    setShowMore(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!panel && !showMore) return undefined;
    const onMouseDown = (event) => {
      if (panelRef.current?.contains(event.target) || event.target.closest?.(".appNav, .mobileTopbar, .mobileTabbar")) return;
      setPanel(null);
      setShowMore(false);
    };
    const onKeyDown = (event) => event.key === "Escape" && setPanel(null);
    document.addEventListener("mousedown", onMouseDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onMouseDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [panel, showMore]);

  const togglePanel = (name) => setPanel((current) => (current === name ? null : name));
  const handleRead = useCallback(() => setUnreadCount(0), []);
  const activityBadge = unreadCount + requestCount;
  const [isDark, setIsDark] = useState(() => document.documentElement.dataset.theme === "dark");
  const toggleAppearance = () => {
    const next = isDark ? "light" : "dark";
    applyTheme(next);
    setIsDark(!isDark);
    if (user.id) updateSettings(user.id, { Theme: next }).catch(() => {});
  };
  const isOwnProfile = location.pathname === "/profile" || location.pathname === `/profile/${user.id}`;

  const navItem = (icon, activeIcon, label, { to, onClick, isActive, badge } = {}) => {
    const content = (active) => (
      <>
        <span className="navIcon">
          {active ? activeIcon : icon}
          {badge > 0 && <span key={badge} className="navBadge">{badge > 99 ? "99+" : badge}</span>}
        </span>
        <span className="navLabel">{label}</span>
      </>
    );
    if (to) {
      return (
        <NavLink to={to} end className={({ isActive: routeActive }) => `navItem ${routeActive && !panel ? "isActive" : ""}`}>
          {({ isActive: routeActive }) => content(routeActive && !panel)}
        </NavLink>
      );
    }
    return (
      <button type="button" className={`navItem ${isActive ? "isActive" : ""}`} onClick={onClick}>
        {content(isActive)}
      </button>
    );
  };

  const profileIcon = (active) => (
    <Avatar src={user.image} size={24} className={`navProfileImg ${active ? "isActive" : ""}`} />
  );

  return (
    <>
      <header className="mobileTopbar">
        <Link to="/"><AppLogo compact /></Link>
        <div className="mobileTopbarActions">
          <button type="button" className="igIconButton" onClick={() => togglePanel("notifications")} aria-label="Notifications">
            <span className="navIcon">
              {panel === "notifications" ? <Favorite /> : <FavoriteBorder />}
              {activityBadge > 0 && <span className="navDot" />}
            </span>
          </button>
          <Link to="/chat" className="igIconButton" aria-label="Messages"><SendOutlined /></Link>
        </div>
      </header>

      <nav className={`appNav ${panel ? "isCollapsed" : ""}`} aria-label="Main">
        <Link to="/" className="navLogo">
          <AppLogo compact />
        </Link>

        <div className="navItems">
          {navItem(<HomeOutlined />, <HomeFilled />, "Home", { to: "/" })}
          {navItem(<Search />, <SearchRounded />, "Search", {
            onClick: () => togglePanel("search"),
            isActive: panel === "search",
          })}
          {navItem(<ExploreOutlined />, <ExploreFilled />, "Explore", { to: "/explore" })}
          {navItem(<AutoAwesomeRounded />, <AutoAwesomeRounded />, "AI Studio", { to: "/studio" })}
          {navItem(<SendOutlined />, <Send />, "Messages", { to: "/chat" })}
          {navItem(<FavoriteBorder />, <Favorite />, "Notifications", {
            onClick: () => togglePanel("notifications"),
            isActive: panel === "notifications",
            badge: activityBadge,
          })}
          {navItem(<AddBoxOutlined />, <AddBoxOutlined />, "Create", {
            onClick: () => { setPanel(null); setShowCreate("choose"); },
          })}
          <NavLink to="/profile" className={`navItem ${isOwnProfile && !panel ? "isActive" : ""}`}>
            <span className="navIcon">{profileIcon(isOwnProfile && !panel)}</span>
            <span className="navLabel">Profile</span>
          </NavLink>
        </div>

        <div className="navMore">
          {showMore && (
            <div className="navMoreMenu">
              <Link to="/settings" className="navMoreItem">
                <SettingsOutlined /> Settings
              </Link>
              <button type="button" className="navMoreItem" onClick={toggleAppearance}>
                {isDark ? <LightModeOutlined /> : <DarkModeOutlined />} Switch appearance
              </button>
              <Link to="/requests" className="navMoreItem">
                <PeopleAltOutlined /> Friend requests
                {requestCount > 0 && <span className="navMoreCount">{requestCount}</span>}
              </Link>
              <button type="button" className="navMoreItem" onClick={logout}>
                <LogoutRounded /> Log out
              </button>
            </div>
          )}
          {navItem(<MenuRounded />, <MenuRounded />, "More", {
            onClick: () => setShowMore((open) => !open),
            isActive: showMore,
          })}
        </div>
      </nav>

      <section
        ref={panelRef}
        className={`navPanel ${panel ? "isOpen" : ""}`}
        aria-hidden={!panel}
      >
        {panel === "search" && (
          <SearchPanel currentUserId={user.id} onNavigate={() => setPanel(null)} />
        )}
        {panel === "notifications" && (
          <NotificationsPanel
            currentUserId={user.id}
            requestCount={requestCount}
            onNavigate={() => setPanel(null)}
            onRead={handleRead}
          />
        )}
      </section>

      <nav className="mobileTabbar" aria-label="Main">
        <NavLink to="/" end className="mobileTab">{({ isActive }) => (isActive ? <HomeFilled /> : <HomeOutlined />)}</NavLink>
        <button type="button" className="mobileTab" onClick={() => togglePanel("search")} aria-label="Search">
          <Search />
        </button>
        <NavLink to="/explore" className="mobileTab" aria-label="Explore">
          {({ isActive }) => (isActive ? <ExploreFilled /> : <ExploreOutlined />)}
        </NavLink>
        <button type="button" className="mobileTab" onClick={() => setShowCreate("choose")} aria-label="Create">
          <AddBoxOutlined />
        </button>
        <NavLink to="/requests" className="mobileTab" aria-label="Friend requests">
          <span className="navIcon">
            <PeopleAltOutlined />
            {requestCount > 0 && <span className="navDot" />}
          </span>
        </NavLink>
        <NavLink to="/profile" className="mobileTab" aria-label="Profile">
          {profileIcon(isOwnProfile)}
        </NavLink>
      </nav>

      {showCreate === "choose" && (
        <AnimatedModal onClose={() => setShowCreate(false)} showClose={false} className="createChooser" label="Create">
          {(close) => (
            <>
              <h2>Create</h2>
              <button type="button" className="createChoice igFadeUp" onClick={() => { close(); setTimeout(() => setShowCreate("post"), 200); }}>
                <span className="createChoiceIcon"><GridOnRounded /></span>
                <span><strong>Post</strong><small>Share a photo, video or thought to your feed and profile.</small></span>
              </button>
              <button type="button" className="createChoice igFadeUp" style={{ animationDelay: "60ms" }} onClick={() => { close(); setTimeout(() => setShowCreate("story"), 200); }}>
                <span className="createChoiceIcon isStory"><AutoAwesomeRounded /></span>
                <span><strong>Story</strong><small>Share a moment that disappears after 24 hours.</small></span>
              </button>
              <button type="button" className="createChoice igFadeUp" style={{ animationDelay: "120ms" }} onClick={() => { close(); navigate("/studio"); }}>
                <span className="createChoiceIcon isAi"><AutoAwesomeRounded /></span>
                <span><strong>AI image</strong><small>Describe it and let Flux paint it in AI Studio.</small></span>
              </button>
            </>
          )}
        </AnimatedModal>
      )}
      {showCreate === "post" && <CreatePostModal onClose={() => setShowCreate(false)} />}
      {showCreate === "story" && <StoryComposer onClose={() => setShowCreate(false)} />}
    </>
  );
}
