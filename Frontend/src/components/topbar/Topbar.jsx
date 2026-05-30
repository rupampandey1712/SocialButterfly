import "./topbar.css";
import { Search, Person, Chat } from "@mui/icons-material";
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import { useState, useEffect } from "react";
import axios from "axios";
import { Link, useNavigate } from "react-router-dom";
import { common } from "@mui/material/colors";
import AppLogo from "../logo/AppLogo";

export default function Topbar() {
  const navigate = useNavigate();
  const logout = () => {
    sessionStorage.removeItem("tokens");
    sessionStorage.removeItem("isLoggedIn");
    sessionStorage.removeItem("Id");
    window.location.reload();
    navigate('/');
  }

  const [friendRequestsCount, setFriendRequestsCount] = useState(0);
  const id = sessionStorage.getItem("Id");
  const image = sessionStorage.getItem('url');
  const [searchTerm, setSearchTerm] = useState('');
  const [users, setUsers] = useState([]);

  useEffect(() => {
    const fetchFriendRequestsCount = async () => {
      try {
        const response = await axios.get(`https://localhost:7151/api/Friends/${id}/requests`);
        console.log(response.data.value);
        setFriendRequestsCount(response.data.value);
      } catch (error) {
        console.log(error);
      }
    };
    fetchFriendRequestsCount();
  }, [id]);

  const handleSearch = async () => {
    try {
      const response = await axios.get(`https://localhost:7151/api/Account/Search?Name=${searchTerm}&Email=${searchTerm}&Role=${searchTerm}&Information=${searchTerm}`);
      setUsers(response.data);
    } catch (error) {
      console.error('Error fetching users:', error);
    }
  };

  const handleInputChange = (event) => {
    setSearchTerm(event.target.value);
  };

  const handleOutsideClick = () => {
    setSearchTerm('');
    setUsers([]);
  };

  useEffect(() => {
    document.addEventListener('click', handleOutsideClick);
    return () => {
      document.removeEventListener('click', handleOutsideClick);
    };
  }, []);

  useEffect(() => {
    return () => {
      setUsers([]); // Clear search results when component is unmounted
    };
  }, []);

  return (
    <div className="topbarContainer">
      <div className="topbarLeft">
        <Link to='/'>
          <AppLogo compact />
        </Link>
      </div>
      <div className="topbarCenter">
        <div className="searchbar">
          <Search className="searchIcon" />
          <input
            placeholder="Search for friend, post or video"
            className="searchInput"
            value={searchTerm}
            onChange={handleInputChange}
          />
        </div>
        {users.length > 0 && (
          <div className="mt-60">
            <div className="card">
              <div className="card-header">
                <h5 className="card-title">Search Results</h5>
              </div>
              <div className="card-body">
                <div className="row">
                  {users.map((user) => (
                    <div key={user.id} className="col-md-4  mb-3">
                      <div className="card" style={{width:'100px'}}>
                        <img
                          src={user.imagePath}
                          className="card-img-top"
                          alt={user.name}
                          style={{ height: '100px', objectFit: 'cover' }}
                        />
                        <div className="card-body">
                          <h5 className="card-title">{user.name}</h5>
                          {/* <p className="card-text">
                            {user.email} - {user.role} - {user.information}
                          </p> */}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
      <div className="topbarRight">
        <div className="topbarLinks">
          <button className="btn btn-primary" onClick={handleSearch}>
            Search
          </button>
          <Link to="/home">
            <span className="topbarLink">Homepage</span>
          </Link>
          <span className="topbarLink">Timeline</span>
          <button className="btn btn-primary" onClick={logout}>
            Logout
          </button>
        </div>
        <div className="topbarIcons">
          <div className="topbarIconItem">
            <Person />
            <span className="topbarIconBadge"></span>
          </div>
          <div className="topbarIconItem">
            <Chat />
            <span className="topbarIconBadge"> </span>
          </div>
          <div className="topbarIconItem">
            <Link to="/requests">
              <NotificationsActiveIcon sx={{ color: common['white'] }} />
            </Link>
            {friendRequestsCount > 0 && (
              <span className="topbarIconBadge">{friendRequestsCount}</span>
            )}
          </div>
        </div>
        <Link to="/">
          <img src={image} alt="" className="topbarImg" />
        </Link>
      </div>
    </div>
  );
}
