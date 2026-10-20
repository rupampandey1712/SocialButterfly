import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { SearchRounded } from "@mui/icons-material";
import AnimatedModal from "../modal/AnimatedModal";
import FollowButton from "./FollowButton";
import { fetchFollowList } from "../../utils/account";
import { getCurrentUser } from "../../utils/session";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import { formatCount } from "../../utils/time";
import "./follow.css";

const tabs = [
  { key: "followers", label: "Followers" },
  { key: "following", label: "Following" },
];

/** Followers / Following lists with a sliding tab indicator and search. */
export default function FollowListModal({ userId, initialTab = "followers", counts, onClose, onCountsChange }) {
  const viewer = getCurrentUser();
  const [tab, setTab] = useState(initialTab);
  const [lists, setLists] = useState({});
  const [query, setQuery] = useState("");

  useEffect(() => {
    if (lists[tab]) return;
    let isActive = true;
    fetchFollowList(userId, tab, viewer.id)
      .then((data) => isActive && setLists((prev) => ({ ...prev, [tab]: data })))
      .catch(() => isActive && setLists((prev) => ({ ...prev, [tab]: [] })));
    return () => { isActive = false; };
  }, [tab, userId, viewer.id, lists]);

  const rows = useMemo(() => {
    const list = lists[tab];
    if (!list) return null;
    const term = query.trim().toLowerCase().replace(/^@/, "");
    if (!term) return list;
    return list.filter((u) => u.name?.toLowerCase().includes(term) || u.username?.toLowerCase().includes(term));
  }, [lists, tab, query]);

  return (
    <AnimatedModal onClose={onClose} showClose={false} className="followModal" label="Followers and following">
      {(close) => (
        <>
          <header className="followModalTabs" role="tablist">
            {tabs.map((t) => (
              <button
                key={t.key}
                type="button"
                role="tab"
                aria-selected={tab === t.key}
                className={tab === t.key ? "isActive" : ""}
                onClick={() => { setTab(t.key); setQuery(""); }}
              >
                {counts?.[t.key] !== undefined && <strong>{formatCount(counts[t.key])}</strong>} {t.label}
              </button>
            ))}
            <span className="followModalIndicator" style={{ transform: `translateX(${tab === "following" ? 100 : 0}%)` }} />
            <button type="button" className="followModalClose igIconButton" onClick={close} aria-label="Close">✕</button>
          </header>

          <div className="followModalSearch">
            <SearchRounded />
            <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search" aria-label="Search list" />
          </div>

          <div key={tab} className="followModalBody">
            {rows === null && <div className="igSpinner" />}
            {rows?.length === 0 && (
              <div className="followModalEmpty igFadeIn">
                {query ? "No results." : tab === "followers" ? "No followers yet." : "Not following anyone yet."}
              </div>
            )}
            {rows?.map((u, i) => (
              <div key={u.id} className="followRow igFadeUp" style={{ animationDelay: `${Math.min(i, 12) * 30}ms` }}>
                <Link to={`/profile/${u.id}`} className="followRowUser" onClick={close}>
                  <img
                    className="igAvatar"
                    width={44}
                    height={44}
                    src={getPublicImageUrl(u.imagePath)}
                    alt=""
                    onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                  />
                  <span>
                    <strong>{u.username || u.name}</strong>
                    <span>{u.name}{u.followsYou && !u.isSelf ? " · Follows you" : ""}</span>
                  </span>
                </Link>
                <FollowButton
                  user={u}
                  isFollowing={u.isFollowing}
                  followsYou={u.followsYou}
                  size="small"
                  onChange={(isFollowing) => {
                    // Following someone from your own "followers" list changes your following count.
                    if (userId === viewer.id) {
                      onCountsChange?.((prev) => ({ ...prev, following: prev.following + (isFollowing ? 1 : -1) }));
                    }
                  }}
                />
              </div>
            ))}
          </div>
        </>
      )}
    </AnimatedModal>
  );
}
