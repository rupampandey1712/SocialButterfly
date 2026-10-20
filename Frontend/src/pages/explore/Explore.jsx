import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { SearchRounded, TravelExploreRounded } from "@mui/icons-material";
import Topbar from "../../components/topbar/Topbar";
import FollowButton from "../../components/follow/FollowButton";
import { fetchPeople } from "../../utils/account";
import { getCurrentUser } from "../../utils/session";
import { formatCount } from "../../utils/time";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import "./explore.css";

/** Discover people: everyone on SocialButterfly, most-followed first, with live search. */
export default function Explore() {
  const viewer = getCurrentUser();
  const [query, setQuery] = useState("");
  const [people, setPeople] = useState(null);

  useEffect(() => {
    let isActive = true;
    const timer = setTimeout(() => {
      fetchPeople(viewer.id, query.trim())
        .then((data) => isActive && setPeople(data))
        .catch(() => isActive && setPeople([]));
    }, query ? 300 : 0);
    return () => { isActive = false; clearTimeout(timer); };
  }, [query, viewer.id]);

  const others = people?.filter((p) => !p.isSelf) ?? null;

  return (
    <>
      <Topbar />
      <div className="explorePage">
        <header className="exploreHeader igFadeUp">
          <span className="exploreIcon"><TravelExploreRounded /></span>
          <div>
            <h1>Discover people</h1>
            <p>Find friends, creators and new faces to follow.</p>
          </div>
        </header>

        <div className="exploreSearch igFadeUp" style={{ animationDelay: "60ms" }}>
          <SearchRounded />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search by name, @username or bio"
            aria-label="Search people"
          />
        </div>

        <div className="exploreGrid">
          {others === null &&
            Array.from({ length: 8 }, (_, i) => (
              <div key={i} className="personCard">
                <div className="skeleton" style={{ width: 88, height: 88, borderRadius: "50%" }} />
                <div className="skeleton" style={{ width: 120, height: 14 }} />
                <div className="skeleton" style={{ width: 80, height: 10 }} />
              </div>
            ))}

          {others?.length === 0 && (
            <p className="exploreEmpty igFadeIn">No one matches “{query}”.</p>
          )}

          {others?.map((person, i) => (
            <article key={person.id} className="personCard igFadeUp" style={{ animationDelay: `${Math.min(i, 12) * 40}ms` }}>
              <Link to={`/profile/${person.id}`} className="personLink">
                <span className="personAvatar">
                  <img
                    className="igAvatar"
                    src={getPublicImageUrl(person.imagePath)}
                    alt=""
                    onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                  />
                </span>
                <strong>{person.name}</strong>
                <span className="personHandle">@{person.username}</span>
                {person.information && <span className="personBio">{person.information}</span>}
                <span className="personMeta">
                  {formatCount(person.followersCount)} {person.followersCount === 1 ? "follower" : "followers"}
                  {person.followsYou && " · Follows you"}
                </span>
              </Link>
              <FollowButton user={person} isFollowing={person.isFollowing} followsYou={person.followsYou} />
            </article>
          ))}
        </div>
      </div>
    </>
  );
}
