import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import axios from "axios";
import {
  ChevronLeftRounded,
  ChevronRightRounded,
  CloseRounded,
  DeleteOutlineRounded,
  FavoriteBorderRounded,
  FavoriteRounded,
  PauseRounded,
  PlayArrowRounded,
  SendRounded,
  VisibilityOutlined,
  VolumeOffRounded,
  VolumeUpRounded,
} from "@mui/icons-material";
import AppLogo from "../logo/AppLogo";
import PostSoundtrack from "../media/PostSoundtrack";
import { useGlobalMute } from "../media/useMediaPlayback";
import { playback } from "../../utils/media";
import { showToast } from "../toast/Toaster";
import { getApiUrl } from "../../utils/api";
import { getCurrentUser } from "../../utils/session";
import { getMediaType } from "../../utils/media";
import { timeAgo } from "../../utils/time";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import {
  deleteStory,
  fetchStoryViewers,
  markStoryViewed,
  STORY_IMAGE_DURATION_MS,
} from "../../utils/stories";
import "./storyViewer.css";

/** Starts on the first unseen story of a group, like Instagram. */
function firstUnseen(group) {
  const index = group.stories.findIndex((s) => !s.seen);
  return index === -1 ? 0 : index;
}

export default function StoryViewer({ groups, startIndex, onClose, onSeen }) {
  const viewer = getCurrentUser();
  const [groupIndex, setGroupIndex] = useState(startIndex);
  const [storyIndex, setStoryIndex] = useState(() => firstUnseen(groups[startIndex]));
  const [direction, setDirection] = useState("next");
  const [isPaused, setIsPaused] = useState(false);
  const [isLoaded, setIsLoaded] = useState(false);
  const [duration, setDuration] = useState(STORY_IMAGE_DURATION_MS);
  // Same global mute as feed videos and music.
  const [isMuted, toggleMuted] = useGlobalMute();
  const [reply, setReply] = useState("");
  const [liked, setLiked] = useState({});
  const [viewers, setViewers] = useState(null);
  const [isClosing, setIsClosing] = useState(false);
  const holdTimer = useRef(null);
  const wasHold = useRef(false);
  const videoRef = useRef(null);

  const group = groups[groupIndex];
  const story = group?.stories[storyIndex];
  const mediaType = getMediaType(story?.mediaPath);
  const isOwn = group?.isOwn;
  const isTyping = reply.length > 0;
  const effectivePaused = isPaused || !isLoaded || isTyping || viewers !== null;
  const hasMusic = Boolean(story?.musicPath);

  // Opening a story is a tap, so sound can start right away (as on Instagram).
  useEffect(() => {
    playback.setMuted(false);
  }, []);

  const close = useCallback(() => {
    setIsClosing(true);
    setTimeout(onClose, 220);
  }, [onClose]);

  const goToGroup = useCallback((index, dir) => {
    if (index < 0) return;
    if (index >= groups.length) {
      close();
      return;
    }
    setDirection(dir);
    setGroupIndex(index);
    setStoryIndex(dir === "prev" ? 0 : firstUnseen(groups[index]));
  }, [groups, close]);

  const next = useCallback(() => {
    if (storyIndex < group.stories.length - 1) {
      setStoryIndex((i) => i + 1);
    } else {
      goToGroup(groupIndex + 1, "next");
    }
  }, [storyIndex, group, groupIndex, goToGroup]);

  const prev = useCallback(() => {
    if (storyIndex > 0) {
      setStoryIndex((i) => i - 1);
    } else if (groupIndex > 0) {
      goToGroup(groupIndex - 1, "prev");
    }
  }, [storyIndex, groupIndex, goToGroup]);

  // Reset per-story state and record the view.
  useEffect(() => {
    if (!story) return;
    setIsLoaded(false);
    setDuration(STORY_IMAGE_DURATION_MS);
    setReply("");
    if (!isOwn && !story.seen && viewer.id) {
      markStoryViewed(story.id, viewer.id).catch(() => {});
      onSeen?.(group.user.id, story.id);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [story?.id]);

  // Keep the video element in sync with pause state.
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    if (effectivePaused) video.pause();
    else video.play().catch(() => {});
  }, [effectivePaused, story?.id]);

  useEffect(() => {
    const onKey = (event) => {
      if (event.target.tagName === "INPUT") {
        if (event.key === "Escape") event.target.blur();
        return;
      }
      if (event.key === "ArrowRight") next();
      else if (event.key === "ArrowLeft") prev();
      else if (event.key === "Escape") close();
      else if (event.key === " ") {
        event.preventDefault();
        setIsPaused((p) => !p);
      }
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", onKey);
    };
  }, [next, prev, close]);

  // Press and hold pauses; a quick tap navigates.
  const onPointerDown = () => {
    wasHold.current = false;
    holdTimer.current = setTimeout(() => {
      wasHold.current = true;
      setIsPaused(true);
    }, 220);
  };

  const onPointerUp = (event) => {
    clearTimeout(holdTimer.current);
    if (wasHold.current) {
      setIsPaused(false);
      return;
    }
    const rect = event.currentTarget.getBoundingClientRect();
    if (event.clientX - rect.left < rect.width * 0.3) prev();
    else next();
  };

  const sendReply = async (content) => {
    const text = content.trim();
    if (!text) return;
    try {
      await axios.post(
        getApiUrl("/api/Chat"),
        { receiverId: group.user.id, content: `Replied to your story: ${text}` }
      );
      showToast(`Sent to ${group.user.name}`);
      setReply("");
    } catch {
      showToast("Couldn't send your reply");
    }
  };

  const react = () => {
    if (liked[story.id]) return;
    setLiked((prev) => ({ ...prev, [story.id]: true }));
    sendReply("❤️");
  };

  const removeStory = async () => {
    try {
      await deleteStory(story.id, viewer.id);
      showToast("Story deleted");
      const remaining = group.stories.filter((s) => s.id !== story.id);
      if (remaining.length === 0) {
        close();
        return;
      }
      group.stories = remaining;
      setStoryIndex((i) => Math.min(i, remaining.length - 1));
    } catch {
      showToast("Couldn't delete this story");
    }
  };

  const openViewers = async () => {
    setViewers([]);
    try {
      setViewers(await fetchStoryViewers(story.id, viewer.id));
    } catch {
      setViewers([]);
    }
  };

  if (!group || !story) return null;

  const prevGroup = groups[groupIndex - 1];
  const nextGroup = groups[groupIndex + 1];

  return createPortal(
    <div className={`storyViewer ${isClosing ? "isClosing" : ""}`} role="dialog" aria-modal="true" aria-label={`${group.user.name}'s story`}>
      <div className="storyViewerLogo"><AppLogo compact light /></div>
      <button type="button" className="storyViewerClose" onClick={close} aria-label="Close">
        <CloseRounded />
      </button>

      <div className="storyStage">
        {prevGroup && (
          <button type="button" className="storyPreview isPrev" onClick={() => goToGroup(groupIndex - 1, "prev")} aria-label={`${prevGroup.user.name}'s story`}>
            <PreviewCard group={prevGroup} />
          </button>
        )}

        <div key={groupIndex} className={`storyCard enter-${direction}`}>
          <div className="storyProgress">
            {group.stories.map((s, index) => (
              <span key={s.id} className="storyProgressTrack">
                <span
                  key={index === storyIndex ? `${s.id}-active` : s.id}
                  className={`storyProgressFill ${index < storyIndex ? "isDone" : index === storyIndex ? "isActive" : ""}`}
                  style={index === storyIndex ? {
                    animationDuration: `${duration}ms`,
                    animationPlayState: effectivePaused ? "paused" : "running",
                  } : undefined}
                  onAnimationEnd={index === storyIndex ? next : undefined}
                />
              </span>
            ))}
          </div>

          <header className="storyHeader">
            <Link to={`/profile/${group.user.id}`} className="storyAuthor" onClick={close}>
              <img
                className="igAvatar"
                width={32}
                height={32}
                src={getPublicImageUrl(group.user.imagePath)}
                alt=""
                onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
              />
              <strong>{group.username || group.user.name}</strong>
              <time>{timeAgo(story.createdAt)}</time>
            </Link>
            <div className="storyControls">
              {(mediaType === "video" || hasMusic) && (
                <button type="button" onClick={toggleMuted} aria-label={isMuted ? "Unmute" : "Mute"}>
                  {isMuted ? <VolumeOffRounded /> : <VolumeUpRounded />}
                </button>
              )}
              <button type="button" onClick={() => setIsPaused((p) => !p)} aria-label={isPaused ? "Play" : "Pause"}>
                {isPaused ? <PlayArrowRounded /> : <PauseRounded />}
              </button>
              {isOwn && (
                <button type="button" onClick={removeStory} aria-label="Delete story">
                  <DeleteOutlineRounded />
                </button>
              )}
            </div>
          </header>

          <div
            className="storyMedia"
            onPointerDown={onPointerDown}
            onPointerUp={onPointerUp}
            onPointerLeave={() => clearTimeout(holdTimer.current)}
            onContextMenu={(e) => e.preventDefault()}
          >
            {!isLoaded && <div className="storySpinner" />}
            {mediaType === "video" ? (
              <video
                key={story.id}
                ref={videoRef}
                src={story.mediaPath}
                muted={isMuted || hasMusic}
                playsInline
                className={isLoaded ? "isLoaded" : ""}
                onLoadedMetadata={(e) => {
                  setDuration(Math.max(1000, Math.min(60000, e.currentTarget.duration * 1000)));
                  setIsLoaded(true);
                }}
                onError={() => setIsLoaded(true)}
              />
            ) : (
              <img
                key={story.id}
                src={story.mediaPath}
                alt=""
                draggable={false}
                className={isLoaded ? "isLoaded" : ""}
                onLoad={() => setIsLoaded(true)}
                onError={() => setIsLoaded(true)}
              />
            )}
            {story.caption && <p className="storyCaption">{story.caption}</p>}
            {hasMusic && (
              <PostSoundtrack
                key={story.id}
                src={story.musicPath}
                title={story.musicTitle}
                artist={story.musicArtist}
                active={isLoaded}
                paused={effectivePaused}
                showMuteButton={false}
              />
            )}
            {isPaused && <span className="storyPausedBadge"><PauseRounded /></span>}
          </div>

          <footer className="storyFooter">
            {isOwn ? (
              <button type="button" className="storySeenBy" onClick={openViewers}>
                <VisibilityOutlined fontSize="small" />
                {story.viewCount > 0 ? `Seen by ${story.viewCount}` : "No views yet"}
              </button>
            ) : (
              <form className="storyReply" onSubmit={(e) => { e.preventDefault(); sendReply(reply); }}>
                <input
                  value={reply}
                  onChange={(e) => setReply(e.target.value)}
                  placeholder={`Reply to ${group.username || group.user.name}…`}
                  aria-label="Reply to story"
                  maxLength={500}
                />
                {reply.trim() ? (
                  <button type="submit" aria-label="Send reply"><SendRounded /></button>
                ) : (
                  <button type="button" onClick={react} className={liked[story.id] ? "isLiked" : ""} aria-label="Like story">
                    {liked[story.id] ? <FavoriteRounded /> : <FavoriteBorderRounded />}
                  </button>
                )}
              </form>
            )}
          </footer>

          {viewers !== null && (
            <div className="storyViewersSheet" role="dialog" aria-label="Story viewers">
              <header>
                <strong>Viewers</strong>
                <button type="button" onClick={() => setViewers(null)} aria-label="Close viewers"><CloseRounded /></button>
              </header>
              <div className="storyViewersList">
                {viewers.length === 0 && <p>When people view your story, you'll see them here.</p>}
                {viewers.map((v, i) => (
                  <Link key={v.user.id} to={`/profile/${v.user.id}`} className="storyViewerRow igFadeUp" style={{ animationDelay: `${i * 35}ms` }} onClick={close}>
                    <img className="igAvatar" width={40} height={40} src={getPublicImageUrl(v.user.imagePath)} alt=""
                      onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }} />
                    <span><strong>{v.user.name}</strong><time>{timeAgo(v.viewedAt)}</time></span>
                  </Link>
                ))}
              </div>
            </div>
          )}

          {(storyIndex > 0 || groupIndex > 0) && (
            <button type="button" className="storyNav isPrev" onClick={prev} aria-label="Previous"><ChevronLeftRounded /></button>
          )}
          <button type="button" className="storyNav isNext" onClick={next} aria-label="Next"><ChevronRightRounded /></button>
        </div>

        {nextGroup && (
          <button type="button" className="storyPreview isNext" onClick={() => goToGroup(groupIndex + 1, "next")} aria-label={`${nextGroup.user.name}'s story`}>
            <PreviewCard group={nextGroup} />
          </button>
        )}
      </div>
    </div>,
    document.body
  );
}

function PreviewCard({ group }) {
  const cover = group.stories[firstUnseen(group)];
  const isVideo = getMediaType(cover?.mediaPath) === "video";
  return (
    <span className="storyPreviewCard">
      {isVideo ? <video src={cover.mediaPath} muted preload="metadata" /> : <img src={cover?.mediaPath} alt="" />}
      <span className="storyPreviewInfo">
        <span className={`storyPreviewRing ${group.hasUnseen ? "" : "isSeen"}`}>
          <img className="igAvatar" src={getPublicImageUrl(group.user.imagePath)} alt=""
            onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }} />
        </span>
        <strong>{group.username || group.user.name}</strong>
        <time>{timeAgo(group.latestAt)}</time>
      </span>
    </span>
  );
}
