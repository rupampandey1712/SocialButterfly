import "./profile.css";
import Topbar from "../../components/topbar/Topbar";
import Sidebar from "../../components/sidebar/Sidebar";
import Feed from "../../components/feed/Feed";
import Rightbar from "../../components/rightbar/Rightbar";
import { getPublicImageUrl, DEFAULT_AVATAR } from "../../utils/image";

export default function Profile() {
  const image = getPublicImageUrl(sessionStorage.getItem("url") || sessionStorage.getItem("imagePath"));
  const name=sessionStorage.getItem("name");

  return (
    <>


      <Topbar />
      <div className="profile">
        <Sidebar />
        <div className="profileRight">
          <div className="profileRightTop">
            <div className="profileCover">
              <img
                className="profileCoverImg"
                src="assets/post/3.jpeg"
                alt=""
              />
              <img
                className="profileUserImg"
                src={image}
                alt=""
                onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
              />
            </div>
            <div className="profileInfo">
                <h4 className="profileInfoName">{name}</h4>
                <span className="profileInfoDesc">Hello my friends!</span>
            </div>
          </div>
          <div className="profileRightBottom">
            <Feed />
            
            <Rightbar profile/>
          </div>
        </div>
      </div>
    </>
  );
}
