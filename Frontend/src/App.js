import { Routes,Route, Navigate, useLocation } from "react-router-dom";
import Profile from "./pages/profile/Profile";
import Register from "./pages/register/Register";
import MyFriends from "./pages/friends/Requests";
import Home from "./pages/home/Home";
import Login from "./pages/login/Login";
import Chat from "./pages/chat/Chat";
import PostPage from "./pages/post/PostPage";
import Settings from "./pages/settings/Settings";
import Explore from "./pages/explore/Explore";
import Studio from "./pages/studio/Studio";
import Toaster from "./components/toast/Toaster";
import { isLoggedIn } from "./utils/session";
import {Sidenav} from "./Admin/component/SideNav";
import Dashboard  from "./Admin/page/Dashboard";
import AdminSettings from "./Admin/page/Settings";
import Maps from "./Admin/page/Maps"
import Tables from "./Admin/page/Tables"
import Footer from "./Admin/component/Footer"
import Scheduler from "./Admin/page/Scheduler";
import "./Admin/admin.css";

const publicPaths = ["/login", "/register"];

/** Signed-out visitors only see the auth pages. */
function RequireAuth({ children }) {
  const location = useLocation();
  return isLoggedIn() ? children : <Navigate to="/login" replace state={{ from: location.pathname }} />;
}

function App(){

  const isAdmin = sessionStorage.getItem("role");
  const location = useLocation();
  const signedIn = isLoggedIn();

  if (signedIn && publicPaths.includes(location.pathname)) {
    return <Navigate to="/" replace />;
  }

  return(
<>

{isAdmin !== "Admin" || !signedIn ?(

// Keying on the path replays the page-in animation on every navigation.
<div key={location.pathname} className="pageTransition">
<Routes location={location}>
<Route path='/login' element={<Login />} />
<Route path='/register' element={<Register />} />
<Route path='/' element={<RequireAuth><Home /></RequireAuth>} />
<Route path='/home' element={<RequireAuth><Home /></RequireAuth>} />
<Route path='/explore' element={<RequireAuth><Explore /></RequireAuth>} />
<Route path='/studio' element={<RequireAuth><Studio /></RequireAuth>} />
<Route path='/profile' element={<RequireAuth><Profile /></RequireAuth>} />
<Route path='/profile/:userId' element={<RequireAuth><Profile /></RequireAuth>} />
<Route path='/p/:postId' element={<RequireAuth><PostPage /></RequireAuth>} />
<Route path='/settings' element={<RequireAuth><Settings /></RequireAuth>} />
<Route path='/requests' element={<RequireAuth><MyFriends /></RequireAuth>} />
<Route path='/chat' element={<RequireAuth><Chat /></RequireAuth>} />
<Route path='*' element={<Navigate to="/" replace />} />
</Routes>
</div>
):(
<div className="admin-layout">
<Sidenav />
            <div className="md:ml-64">
                <Routes>
                    <Route path="/" element={<Dashboard/>} />
                    <Route path="/settings" element={<AdminSettings/>} />
                    <Route path="/tables" element={<Tables/>} />
                    <Route path="/maps" element={<Maps/>} />
                    <Route path="/calendar" element={<Scheduler/>} />
                    {/* <Redirect from="*" to="/" /> */}
                </Routes>
                <Footer />
            </div>
            </div>

)}

<Toaster />

      </>
  )
}

export default App;
