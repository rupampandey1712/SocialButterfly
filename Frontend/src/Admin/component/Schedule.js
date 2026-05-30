import { useState, useEffect } from 'react';
import Calendar from 'react-calendar';
import 'react-calendar/dist/Calendar.css';
import axios from 'axios';
import './Schedule.css';
 
function Schedule() {
  const [date, setDate] = useState(new Date());
  const [events, setEvents] = useState([]);
 
  useEffect(() => {
    fetchPosts();
  }, [date]);
 
  const fetchPosts = async () => {
    try {
      const response = await axios.get(`https://localhost:7151/api/Admin/PostByDate`);
      const posts = response.data;
      const formattedEvents = posts.map((post) => ({
        title: `${post.name}`,
        date: new Date(post.createdAt),
        userProfilePicture: post.image,
        userName: post.name,
      }));
      setEvents(formattedEvents);
    } catch (error) {
      console.error('Error fetching posts:', error);
    }
  };
 
  const tileContent = ({ date, view }) => {
    if (view !== 'month') {
      return null;
    }

    const eventsForDate = events.filter((event) => event.date.toDateString() === date.toDateString());
 
    if (eventsForDate.length > 0) {
      return (
<div className="tile-content">
          <span className="admin-event-dot">{eventsForDate.length} post{eventsForDate.length > 1 ? 's' : ''}</span>
</div>
      );
    }
    return null;
  };
 
  return (
<div className="admin-card admin-calendar-card">
<div className="d-flex flex-column flex-md-row align-items-md-center justify-content-between gap-2 mb-3">
<div>
<p className="admin-card-kicker">Publishing</p>
<h2 className="admin-card-title">Content Calendar</h2>
</div>
<p className="text-muted mb-0">
<span className="fw-semibold">Selected:</span>{' '}
        {date.toDateString()}
</p>
</div>
<div className="calendar-container">
<Calendar
          onChange={setDate}
          value={date}
          tileContent={tileContent}
        />
</div>
</div>
  );
}
 
export default Schedule;
