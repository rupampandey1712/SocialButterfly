import { useCallback, useEffect, useRef, useState } from "react";
import { AddRounded, ChevronLeftRounded, ChevronRightRounded } from "@mui/icons-material";
import StoryViewer from "./StoryViewer";
import StoryComposer from "./StoryComposer";
import { getCurrentUser } from "../../utils/session";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import { fetchStoryFeed, STORIES_CHANGED_EVENT } from "../../utils/stories";
import "./stories.css";

/**
 * Instagram stories tray: your story first, then unseen stories (gradient ring),
 * then seen ones (grey ring). Tapping opens the full-screen viewer.
 */
export default function Stories() {
  const user = getCurrentUser();
  const [groups, setGroups] = useState(null);
  const [viewerIndex, setViewerIndex] = useState(null);
  const [showComposer, setShowComposer] = useState(false);
  const [scroll, setScroll] = useState({ start: true, end: true });
  const railRef = useRef(null);

  const load = useCallback(async () => {
    if (!user.id) {
      setGroups([]);
      return;
    }
    try {
      setGroups(await fetchStoryFeed(user.id));
    } catch {
      setGroups([]);
    }
  }, [user.id]);

  useEffect(() => {
    load();
    window.addEventListener(STORIES_CHANGED_EVENT, load);
    return () => window.removeEventListener(STORIES_CHANGED_EVENT, load);
  }, [load]);

  const updateScroll = () => {
    const rail = railRef.current;
    if (!rail) return;
    setScroll({
      start: rail.scrollLeft <= 4,
      end: rail.scrollLeft + rail.clientWidth >= rail.scrollWidth - 4,
    });
  };

  useEffect(updateScroll, [groups]);

  const ownGroup = groups?.find((g) => g.isOwn);
  const others = groups?.filter((g) => !g.isOwn) ?? [];
  // Viewer order matches the tray: own story (if any) first.
  const viewerGroups = groups ?? [];

  const openGroup = (group) => setViewerIndex(viewerGroups.indexOf(group));

  const markGroupSeen = useCallback((userId, storyId) => {
    setGroups((prev) => prev?.map((g) => {
      if (g.user.id !== userId) return g;
      const stories = g.stories.map((s) => (s.id === storyId ? { ...s, seen: true } : s));
      return { ...g, stories, hasUnseen: stories.some((s) => !s.seen) };
    }));
  }, []);

  return (
    <div className="stories">
      {!scroll.start && (
        <button type="button" className="storiesArrow left" onClick={() => railRef.current?.scrollBy({ left: -320, behavior: "smooth" })} aria-label="Scroll left">
          <ChevronLeftRounded />
        </button>
      )}

      <div className="storiesRail" ref={railRef} onScroll={updateScroll}>
        <div className="story igFadeUp">
          <button
            type="button"
            className={`storyRing ${ownGroup ? (ownGroup.hasUnseen ? "" : "isSeen") : "isOwn"}`}
            onClick={() => (ownGroup ? openGroup(ownGroup) : setShowComposer(true))}
            aria-label={ownGroup ? "View your story" : "Add to your story"}
          >
            <img className="igAvatar" src={user.image} alt="" onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }} />
          </button>
          <button
            type="button"
            className="storyAddBadge"
            onClick={() => setShowComposer(true)}
            aria-label="Add to your story"
          >
            <AddRounded />
          </button>
          <span className="storyName">Your story</span>
        </div>

        {groups === null
          ? [0, 1, 2, 3, 4, 5].map((i) => (
              <div key={i} className="story">
                <span className="skeleton storySkeleton" />
                <span className="skeleton" style={{ width: 48, height: 10 }} />
              </div>
            ))
          : others.map((group, i) => (
              <div key={group.user.id} className="story igFadeUp" style={{ animationDelay: `${Math.min(i + 1, 10) * 45}ms` }}>
                <button
                  type="button"
                  className={`storyRing ${group.hasUnseen ? "" : "isSeen"}`}
                  onClick={() => openGroup(group)}
                  aria-label={`View ${group.user.name}'s story`}
                >
                  <img
                    className="igAvatar"
                    src={getPublicImageUrl(group.user.imagePath)}
                    alt=""
                    onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                  />
                </button>
                <span className="storyName">{group.username || group.user.name}</span>
              </div>
            ))}

        {groups !== null && others.length === 0 && (
          <div className="storiesEmpty igFadeIn">Follow people to see their stories here.</div>
        )}
      </div>

      {!scroll.end && (
        <button type="button" className="storiesArrow right" onClick={() => railRef.current?.scrollBy({ left: 320, behavior: "smooth" })} aria-label="Scroll right">
          <ChevronRightRounded />
        </button>
      )}

      {viewerIndex !== null && viewerGroups[viewerIndex] && (
        <StoryViewer
          groups={viewerGroups}
          startIndex={viewerIndex}
          onSeen={markGroupSeen}
          onClose={() => { setViewerIndex(null); load(); }}
        />
      )}
      {showComposer && <StoryComposer onClose={() => setShowComposer(false)} />}
    </div>
  );
}
