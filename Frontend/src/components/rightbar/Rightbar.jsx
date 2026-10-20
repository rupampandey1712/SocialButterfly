import "./rightbar.css";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import FollowButton from "../follow/FollowButton";
import { fetchSuggestions } from "../../utils/account";
import { getCurrentUser, logout } from "../../utils/session";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";

const suggestionLimit = 5;

/** Instagram's right column: the signed-in account plus "Suggested for you". */
export default function Rightbar() {
  const user = getCurrentUser();
  const [suggestions, setSuggestions] = useState(null);

  useEffect(() => {
    if (!user.id) {
      setSuggestions([]);
      return;
    }
    let isActive = true;
    fetchSuggestions(user.id, suggestionLimit)
      .then((data) => isActive && setSuggestions(data))
      // On failure show nothing rather than "you're following everyone".
      .catch(() => isActive && setSuggestions(false));
    return () => { isActive = false; };
  }, [user.id]);

  return (
    <aside className="rightbar">
      <div className="rightbarWrapper">
        <div className="rightbarAccount igFadeIn">
          <Link to="/profile">
            <img
              className="igAvatar"
              width={44}
              height={44}
              src={user.image}
              alt=""
              onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
            />
          </Link>
          <div className="rightbarAccountText">
            <Link to="/profile" className="rightbarName">{user.username || user.name || "Your account"}</Link>
            <span className="rightbarSub">{user.name}</span>
          </div>
          <button type="button" className="igLinkButton rightbarSmall" onClick={logout}>Switch</button>
        </div>

        <div className="rightbarHeading">
          <span>Suggested for you</span>
          <Link to="/explore" className="rightbarSeeAll">See all</Link>
        </div>

        <ul className="rightbarSuggestions">
          {suggestions === null &&
            [0, 1, 2].map((i) => (
              <li key={i} className="rightbarSuggestion">
                <div className="skeleton" style={{ width: 44, height: 44, borderRadius: "50%" }} />
                <div className="rightbarAccountText">
                  <div className="skeleton" style={{ width: 110, height: 12, marginBottom: 6 }} />
                  <div className="skeleton" style={{ width: 70, height: 10 }} />
                </div>
              </li>
            ))}

          {suggestions?.length === 0 && (
            <li className="rightbarSub">You're following everyone. 🎉</li>
          )}

          {suggestions && suggestions.map((candidate, i) => (
            <li
              key={candidate.id}
              className="rightbarSuggestion igFadeUp"
              style={{ animationDelay: `${i * 60}ms` }}
            >
              <Link to={`/profile/${candidate.id}`}>
                <img
                  className="igAvatar"
                  width={44}
                  height={44}
                  src={getPublicImageUrl(candidate.imagePath)}
                  alt=""
                  onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                />
              </Link>
              <div className="rightbarAccountText">
                <Link to={`/profile/${candidate.id}`} className="rightbarName">{candidate.username || candidate.name}</Link>
                <span className="rightbarSub">{candidate.followsYou ? "Follows you" : "Suggested for you"}</span>
              </div>
              <FollowButton
                user={candidate}
                isFollowing={candidate.isFollowing}
                followsYou={candidate.followsYou}
                variant="link"
              />
            </li>
          ))}
        </ul>

        <footer className="rightbarFooter">© {new Date().getFullYear()} SocialButterfly</footer>
      </div>
    </aside>
  );
}
