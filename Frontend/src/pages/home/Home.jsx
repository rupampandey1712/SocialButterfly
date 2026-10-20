import Topbar from "../../components/topbar/Topbar";
import Feeds from "../../components/feed/Feeds";
import Rightbar from "../../components/rightbar/Rightbar";
import "./home.css"


export default function Home() {
  return (
    <>
      <Topbar />
      <div className="homeContainer">
        <Feeds />
        <Rightbar />
      </div>
    </>
  );
}
