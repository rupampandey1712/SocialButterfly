import { useState } from "react";
import AnimatedModal from "../modal/AnimatedModal";
import { showToast } from "../toast/Toaster";
import { apiError, toggleFollow } from "../../utils/account";
import { getCurrentUser } from "../../utils/session";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import "./follow.css";

/**
 * Follow / Following / Follow back. Unfollowing asks for confirmation, like Instagram.
 * `onChange(isFollowing, followersCount)` lets parents update counts.
 */
export default function FollowButton({ user, isFollowing, followsYou, onChange, size = "regular", variant = "button" }) {
  const viewer = getCurrentUser();
  const [following, setFollowing] = useState(isFollowing);
  const [isBusy, setIsBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [popKey, setPopKey] = useState(0);

  if (!viewer.id || viewer.id === user.id) return null;

  const run = async () => {
    if (isBusy) return;
    const next = !following;
    setFollowing(next);
    setPopKey((k) => k + 1);
    setIsBusy(true);
    try {
      const result = await toggleFollow(user.id, viewer.id);
      setFollowing(result.isFollowing);
      onChange?.(result.isFollowing, result.followersCount);
    } catch (error) {
      setFollowing(!next);
      showToast(apiError(error, "Couldn't update follow"));
    } finally {
      setIsBusy(false);
    }
  };

  const label = following ? "Following" : followsYou ? "Follow back" : "Follow";
  const className = variant === "link"
    ? `followLink ${following ? "isFollowing" : ""}`
    : `igButton followButton ${following ? "secondary isFollowing" : ""} ${size === "small" ? "isSmall" : ""}`;

  return (
    <>
      <button
        key={popKey}
        type="button"
        className={`${className} ${popKey ? "followPop" : ""}`}
        onClick={() => (following ? setConfirming(true) : run())}
        disabled={isBusy}
      >
        {label}
      </button>

      {confirming && (
        <AnimatedModal onClose={() => setConfirming(false)} showClose={false} className="unfollowSheet" label="Unfollow">
          {(close) => (
            <>
              <div className="unfollowHeader">
                <img
                  className="igAvatar"
                  width={90}
                  height={90}
                  src={getPublicImageUrl(user.imagePath)}
                  alt=""
                  onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                />
                <p>Unfollow @{user.username || user.name}?</p>
              </div>
              <button type="button" className="postOption danger" onClick={() => { close(); run(); }}>Unfollow</button>
              <button type="button" className="postOption" onClick={close}>Cancel</button>
            </>
          )}
        </AnimatedModal>
      )}
    </>
  );
}
