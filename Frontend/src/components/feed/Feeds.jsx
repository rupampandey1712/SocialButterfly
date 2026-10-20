import "./feed.css";
import AllPost from "../post/allPosts";
import Stories from "../stories/Stories";


export default function Feeds() {
  return (
    <main className="feed">
      <div className="feedWrapper">
        <Stories />
        <AllPost />
      </div>
    </main>
  );
}
