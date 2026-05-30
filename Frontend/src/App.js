import { Routes,Route, useLocation } from "react-router-dom";
import Profile from "./pages/profile/Profile";
import Register from "./pages/register/Register";
import MyFriends from "./pages/friends/Requests";
import Home from "./pages/home/Home";
import Login from "./pages/login/Login";
import {Sidenav} from "./Admin/component/SideNav";
import Dashboard  from "./Admin/page/Dashboard";
import Settings from "./Admin/page/Settings";
import Maps from "./Admin/page/Maps"
import Tables from "./Admin/page/Tables"
import Footer from "./Admin/component/Footer"
import Scheduler from "./Admin/page/Scheduler";
import "./Admin/admin.css";

function App(){

  const isAdmin = sessionStorage.getItem("role");
  const location = useLocation();
  const showLogin = location.pathname !== "/register" && isAdmin !== "Admin";

  return(
<>

{showLogin && <Login/>}

{isAdmin !== "Admin" ?(

<Routes>
<Route path='/' element={<Profile></Profile>} />
<Route path='/register' element={<Register></Register>} />
<Route path='/requests' element={<MyFriends></MyFriends>} />
<Route path='/home' element={<Home></Home>} />
</Routes>
):(
<div className="admin-layout">
<Sidenav />
            <div className="md:ml-64">
                <Routes>
                    <Route path="/" element={<Dashboard/>} />
                    <Route path="/settings" element={<Settings/>} />
                    <Route path="/tables" element={<Tables/>} />
                    <Route path="/maps" element={<Maps/>} />
                    <Route path="/calendar" element={<Scheduler/>} />
                    {/* <Redirect from="*" to="/" /> */}
                </Routes>
                <Footer />
            </div>
            </div>

)}


      </>
  )
}

export default App;
