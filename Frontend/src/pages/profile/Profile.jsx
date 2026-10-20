import "./profile.css";
import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import axios from "axios";
import {
  CameraAltOutlined,
  ChatBubble,
  CheckRounded,
  Favorite,
  GridOnRounded,
  MusicNoteRounded,
  LinkRounded,
  PeopleAltOutlined,
  PersonAddAltRounded,
  PlayArrowRounded,
  SettingsOutlined,
} from "@mui/icons-material";
import Topbar from "../../components/topbar/Topbar";
import PostModal from "../../components/post/PostModal";
import AnimatedModal from "../../components/modal/AnimatedModal";
import FollowButton from "../../components/follow/FollowButton";
import FollowListModal from "../../components/follow/FollowListModal";
import StoryViewer from "../../components/stories/StoryViewer";
import { showToast } from "../../components/toast/Toaster";
import { getApiUrl } from "../../utils/api";
import { getCurrentUser } from "../../utils/session";
import { getMediaType } from "../../utils/media";
import { formatCount } from "../../utils/time";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";
import { fetchUserStories, STORIES_CHANGED_EVENT } from "../../utils/stories";
import {
  acceptFriendRequest,
  fetchProfile,
  fetchUserPosts,
  POST_CREATED_EVENT,
  sendFriendRequest,
} from "../../utils/posts";

function FriendsModal({ userId, onClose }) {
  const [friends, setFriends] = useState(null);

  useEffect(() => {
    axios
      .get(getApiUrl(`/api/Friends/${userId}/myfriends`))
      .then((response) => setFriends(response.data?.value || []))
      .catch(() => setFriends([]));
  }, [userId]);

  return (
    <AnimatedModal onClose={onClose} showClose={false} className="friendsModal" label="Friends">
      {(close) => (
        <>
          <header className="friendsModalHeader">
            <h2>Friends</h2>
            <button type="button" className="igIconButton" onClick={close} aria-label="Close">✕</button>
          </header>
          <div className="friendsModalBody">
            {friends === null && <div className="igSpinner" />}
            {friends?.length === 0 && <p className="profileMuted">No friends yet.</p>}
            {friends?.map((friend, i) => (
              <Link
                key={friend.id}
                to={`/profile/${friend.id}`}
                className="friendsModalRow igFadeUp"
                style={{ animationDelay: `${Math.min(i, 10) * 35}ms` }}
                onClick={close}
              >
                <img
                  className="igAvatar"
                  width={44}
                  height={44}
                  src={getPublicImageUrl(friend.imagePath)}
                  alt=""
                  onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                />
                <strong>{friend.name}</strong>
              </Link>
            ))}
          </div>
        </>
      )}
    </AnimatedModal>
  );
}

export default function Profile() {
  const params = useParams();
  const viewer = getCurrentUser();
  const profileId = Number(params.userId) || viewer.id;
  const isOwn = profileId === viewer.id;

  const [profile, setProfile] = useState(null);
  const [posts, setPosts] = useState(null);
  const [error, setError] = useState(false);
  const [activePostId, setActivePostId] = useState(null);
  const [showFriends, setShowFriends] = useState(false);
  const [followList, setFollowList] = useState(null);
  const [storyGroups, setStoryGroups] = useState(null);
  const [isWorking, setIsWorking] = useState(false);

  const load = useCallback(async () => {
    if (!profileId) return;
    setError(false);
    try {
      const [profileData, postData] = await Promise.all([
        fetchProfile(profileId, viewer.id),
        fetchUserPosts(profileId, viewer.id),
      ]);
      setProfile(profileData);
      setPosts(postData);
    } catch {
      setError(true);
    }
  }, [profileId, viewer.id]);

  useEffect(() => {
    setProfile(null);
    setPosts(null);
    load();
  }, [load]);

  useEffect(() => {
    window.addEventListener(STORIES_CHANGED_EVENT, load);
    if (!isOwn) return () => window.removeEventListener(STORIES_CHANGED_EVENT, load);
    window.addEventListener(POST_CREATED_EVENT, load);
    return () => {
      window.removeEventListener(STORIES_CHANGED_EVENT, load);
      window.removeEventListener(POST_CREATED_EVENT, load);
    };
  }, [isOwn, load]);

  const updatePost = useCallback((updated) => {
    setPosts((prev) => prev.map((p) => (p.id === updated.id ? updated : p)));
  }, []);

  const removePost = useCallback((postId) => {
    setPosts((prev) => prev.filter((p) => p.id !== postId));
    setProfile((prev) => ({ ...prev, postsCount: Math.max(0, prev.postsCount - 1) }));
    setActivePostId(null);
  }, []);

  const openStory = async () => {
    try {
      const groups = await fetchUserStories(profileId, viewer.id);
      if (groups.length) setStoryGroups(groups);
    } catch {
      showToast("Couldn't load this story");
    }
  };

  const friendAction = async () => {
    if (isWorking) return;
    setIsWorking(true);
    try {
      if (profile.friendshipStatus === "RequestReceived") {
        await acceptFriendRequest(profileId, viewer.id);
        setProfile((prev) => ({ ...prev, friendshipStatus: "Friends", friendsCount: prev.friendsCount + 1 }));
        showToast(`You and ${profile.name} are now friends`);
      } else {
        await sendFriendRequest(viewer.id, profileId);
        setProfile((prev) => ({ ...prev, friendshipStatus: "RequestSent" }));
        showToast("Friend request sent");
      }
    } catch {
      showToast("Something went wrong. Try again.");
    } finally {
      setIsWorking(false);
    }
  };

  const copyProfileLink = () => {
    const url = `${window.location.origin}/profile/${profileId}`;
    navigator.clipboard?.writeText(url).then(() => showToast("Profile link copied"), () => showToast(url));
  };

  const activePost = posts?.find((p) => p.id === activePostId);
  const counts = profile && { followers: profile.followersCount, following: profile.followingCount };

  return (
    <>
      <Topbar />
      <div className="profilePage">
        {error ? (
          <div className="profileError igFadeIn">
            <h2>Sorry, this page isn't available.</h2>
            <p className="profileMuted">The profile may have been removed. <Link to="/">Go back to SocialButterfly.</Link></p>
          </div>
        ) : (
          <>
            <header className="profileHeader">
              <div className="profileAvatarCol">
                {profile ? (
                  <button
                    type="button"
                    className={`profileAvatarButton ${profile.hasActiveStory ? "hasStory" : ""}`}
                    onClick={profile.hasActiveStory ? openStory : undefined}
                    aria-label={profile.hasActiveStory ? `View ${profile.name}'s story` : profile.name}
                  >
                    <span className={`profileAvatarRing igFadeIn ${profile.hasActiveStory ? "igRing" : ""}`}>
                      <img
                        className="igAvatar profileAvatar"
                        src={getPublicImageUrl(profile.imagePath)}
                        alt={profile.name || ""}
                        onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                      />
                    </span>
                  </button>
                ) : (
                  <div className="skeleton profileAvatarSkeleton" />
                )}
              </div>

              <section className="profileInfo">
                {profile ? (
                  <>
                    <div className="profileTitleRow igFadeIn">
                      <h1>{profile.username || profile.name}</h1>
                      {isOwn ? (
                        <>
                          <Link to="/settings" className="igButton secondary">Edit profile</Link>
                          <button type="button" className="igButton secondary" onClick={copyProfileLink}>
                            <LinkRounded fontSize="small" /> Share profile
                          </button>
                          <Link to="/settings" className="igIconButton" aria-label="Settings"><SettingsOutlined /></Link>
                        </>
                      ) : (
                        <>
                          <FollowButton
                            key={`${profile.id}-${profile.isFollowing}`}
                            user={profile}
                            isFollowing={profile.isFollowing}
                            followsYou={profile.followsYou}
                            onChange={(isFollowing, followersCount) =>
                              setProfile((prev) => ({ ...prev, isFollowing, followersCount }))
                            }
                          />
                          <Link to={`/chat?userId=${profileId}`} className="igButton secondary">Message</Link>
                          {profile.friendshipStatus === "Friends" ? (
                            <span className="igButton secondary profileStatic" title="You're friends">
                              <CheckRounded fontSize="small" /> Friends
                            </span>
                          ) : profile.friendshipStatus === "RequestSent" ? (
                            <span className="igButton secondary profileStatic">Requested</span>
                          ) : (
                            <button
                              type="button"
                              className="igButton secondary"
                              onClick={friendAction}
                              disabled={isWorking}
                              title={profile.friendshipStatus === "RequestReceived" ? "Accept friend request" : "Add friend"}
                            >
                              <PersonAddAltRounded fontSize="small" />
                              {profile.friendshipStatus === "RequestReceived" ? "Confirm" : ""}
                            </button>
                          )}
                        </>
                      )}
                    </div>

                    <ul className="profileStats igFadeIn">
                      <li><strong>{formatCount(profile.postsCount)}</strong> {profile.postsCount === 1 ? "post" : "posts"}</li>
                      <li>
                        <button type="button" onClick={() => setFollowList("followers")}>
                          <strong key={profile.followersCount} className="countBump">{formatCount(profile.followersCount)}</strong>{" "}
                          {profile.followersCount === 1 ? "follower" : "followers"}
                        </button>
                      </li>
                      <li>
                        <button type="button" onClick={() => setFollowList("following")}>
                          <strong key={profile.followingCount} className="countBump">{formatCount(profile.followingCount)}</strong> following
                        </button>
                      </li>
                    </ul>

                    <div className="profileBioBlock igFadeIn">
                      <strong>{profile.name}</strong>
                      {profile.information && <p className="profileBio">{profile.information}</p>}
                      <button type="button" className="profileFriendsLink" onClick={() => setShowFriends(true)}>
                        <PeopleAltOutlined fontSize="small" /> {formatCount(profile.friendsCount)} {profile.friendsCount === 1 ? "friend" : "friends"}
                      </button>
                      {!isOwn && profile.followsYou && <span className="profileFollowsYou">Follows you</span>}
                    </div>
                  </>
                ) : (
                  <>
                    <div className="skeleton" style={{ width: 180, height: 22, marginBottom: 24 }} />
                    <div className="skeleton" style={{ width: 280, height: 14, marginBottom: 18 }} />
                    <div className="skeleton" style={{ width: 200, height: 12 }} />
                  </>
                )}
              </section>
            </header>

            <div className="profileTabs">
              <span className="profileTab isActive"><GridOnRounded fontSize="small" /> Posts</span>
            </div>

            {posts === null ? (
              <div className="profileGrid">
                {Array.from({ length: 6 }, (_, i) => <div key={i} className="profileTile skeleton" />)}
              </div>
            ) : posts.length === 0 ? (
              <div className="profileEmpty igFadeUp">
                <span className="profileEmptyIcon"><CameraAltOutlined /></span>
                <h2>{isOwn ? "Share photos" : "No posts yet"}</h2>
                <p className="profileMuted">
                  {isOwn ? "When you share photos, they will appear on your profile." : "When they share photos, you'll see them here."}
                </p>
              </div>
            ) : (
              <div className="profileGrid">
                {posts.map((post, i) => {
                  const mediaType = getMediaType(post.imagePath);
                  return (
                    <button
                      key={post.id}
                      type="button"
                      className="profileTile igFadeUp"
                      style={{ animationDelay: `${Math.min(i, 12) * 45}ms` }}
                      onClick={() => setActivePostId(post.id)}
                    >
                      {mediaType === "image" ? (
                        <img src={getPublicImageUrl(post.imagePath, "")} alt="" loading="lazy" />
                      ) : mediaType === "video" ? (
                        <>
                          <video src={getPublicImageUrl(post.imagePath, "")} muted preload="metadata" />
                          <PlayArrowRounded className="profileTileBadge" />
                        </>
                      ) : mediaType === "audio" ? (
                        <span className="profileTileMusic">
                          <span className="profileTileVinyl"><MusicNoteRounded /></span>
                          <strong>{post.musicTitle || post.description || "Original audio"}</strong>
                          {post.musicArtist && <small>{post.musicArtist}</small>}
                        </span>
                      ) : (
                        <span className="profileTileText">{post.description}</span>
                      )}
                      {post.musicPath && mediaType !== "audio" && <MusicNoteRounded className="profileTileBadge" />}
                      <span className="profileTileOverlay">
                        <span><Favorite /> {formatCount(post.likeCount)}</span>
                        <span><ChatBubble /> {formatCount(post.commentCount)}</span>
                      </span>
                    </button>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>

      {activePost && (
        <PostModal post={activePost} onUpdate={updatePost} onDelete={removePost} onClose={() => setActivePostId(null)} />
      )}
      {showFriends && <FriendsModal userId={profileId} onClose={() => setShowFriends(false)} />}
      {followList && profile && (
        <FollowListModal
          userId={profileId}
          initialTab={followList}
          counts={counts}
          onClose={() => { setFollowList(null); load(); }}
          onCountsChange={(update) => setProfile((prev) => {
            const nextCounts = update({ followers: prev.followersCount, following: prev.followingCount });
            return { ...prev, followersCount: nextCounts.followers, followingCount: nextCounts.following };
          })}
        />
      )}
      {storyGroups && (
        <StoryViewer
          groups={storyGroups}
          startIndex={0}
          onClose={() => { setStoryGroups(null); load(); }}
        />
      )}
    </>
  );
}
