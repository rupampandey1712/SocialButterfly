import {Card, Image} from 'react-bootstrap';
import React, { useState, useEffect } from 'react';
import axios from 'axios';


export default function CardTable() {
    const [userData, setUserData] = useState([]);
 
useEffect(() => {
  const fetchUserData = async () => {
    try {
      const response = await axios.get('https://localhost:7151/api/Admin/GetUserDetail');
      setUserData(response.data);
    } catch (error) {
      console.error('Error fetching user data:', error);
    }
  };
 
  fetchUserData();
}, []);

const handleToggle = async(userId, isDeleted) =>{
 
    try{
    const response = await axios.put(`https://localhost:7151/api/Admin?id=${userId}&IsDeleted=${!isDeleted}`)

    if (response.status === 200) {
        setUserData((prevUserData) =>
          prevUserData.map((user) =>
            user.id === userId ? { ...user, isDeleted: !isDeleted } : user
          )
        );
      }
    } catch (error) {
      console.error('Error updating user flag:', error);
    }
  };


    return (
        <Card className="admin-card">
            <Card.Header>
                <p className="admin-card-kicker">Moderation</p>
                <h2 className="admin-card-title">Users</h2>
            </Card.Header>
            <Card.Body>
                <div className="overflow-x-auto">
                    <table className="admin-table table align-middle">
                        <thead>
                            <tr>
                                <th>
                                    Image
                                </th>
                                <th>
                                    Email
                                </th>
                                <th>
                                    Status
                                </th>
                                <th>
                                    Role
                                </th>
                                <th>
                                    Name
                                </th>
                            </tr>
                        </thead>
                        <tbody>
  {userData.length === 0 && (
<tr>
<td className="text-muted py-4" colSpan="5">No user records are available yet.</td>
</tr>
  )}
  {userData.map((user) => (
<tr key={user.id}>

<th>
<Image src={user.imagepath} roundedCircle alt={user.name || 'User'} className='w-10 h-10 object-cover' />
</th>
<td>
        {user.email}
</td>
<td>
<div className="form-switch">                      
<input type="checkbox"
        checked={user.isDeleted}                       
        onChange={() => handleToggle(user.id, user.isDeleted)}                        
        className="form-check-input"
/>                    
</div>
</td>
<td>
        {user.role}
</td>
<td>
        {user.name}
</td>
</tr>
  ))}
</tbody>
                    </table>
                </div>
            </Card.Body>
        </Card>
    );
}
