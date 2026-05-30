import React, { useState, useEffect } from 'react';
import Calendar from 'react-calendar';
import 'react-calendar/dist/Calendar.css';
import axios from 'axios';
import './SchedulerEx.css';
 
const SchedulerEx = () => {
  const [events, setEvents] = useState([]);
 
  useEffect(() => {
    fetchPosts();
  }, []);
 
  const fetchPosts = async () => {
    try {
      const response = await axios.get('https://localhost:7151/api/Admin/PostByDate');
      const posts = response.data;
      const formattedEvents = posts.map((post) => ({
        title: `${post.Name}`,
        date: post.createdAt,
        userProfilePicture: post.userProfilePicture,
      }));
      setEvents(formattedEvents);
    } catch (error) {
      console.error('Error fetching posts:', error);
    }
  };
 
  const tileContent = ({ date, view }) => {
    const eventsForDate = events.filter((event) => event.date.toDateString() === date.toDateString());
 
    if (eventsForDate.length > 0) {
      return (
<div className="tile-content">
          {eventsForDate.map((event) => (
<div key={event.date.toString()} className="event-item">
<img
                src={event.userProfilePicture}
                alt={event.userName}
                className="event-user-image"
              />
<span className="event-user-name">{event.userName}</span>
</div>
          ))}
</div>
      );
    }
    return null;
  };
 
  return (
<div className="scheduler-container">
<Calendar
        tileContent={tileContent}
        calendarType="US"
        navigationLabel={null}
        nextLabel={<span aria-label="Next Month">&#8594;</span>}
        prevLabel={<span aria-label="Previous Month">&#8592;</span>}
      />
</div>
  );
};
 
export default SchedulerEx;