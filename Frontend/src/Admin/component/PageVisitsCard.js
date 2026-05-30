import {Card, Button }from 'react-bootstrap';
import React, {useState, useEffect} from 'react'; 
import axios from 'axios';

export default function PageVisitsCard() {

    const [Userdata, setUserData] = useState([]);
 
    useEffect(() => {
      const fetchUserData = async () => {
        try {
          const response = await axios.get('https://localhost:7151/api/Admin/GetUserDetail');
          const Userdata = await response.data;
          setUserData(Userdata);
        } catch (error) {
          console.error('Error fetching user data:', error);
        }
      };
     
      fetchUserData();
    }, []);

    const headers = Userdata.length > 0 ? Object.keys(Userdata[0]) : [];

    return (
        <Card className="admin-card">
            <Card.Header>
                <div className="w-full flex items-center justify-between">
                    <div>
                        <p className="admin-card-kicker">Recent activity</p>
                        <h2 className="admin-card-title">Page Visits</h2>
                    </div>
                    <Button
                        variant="outline-secondary"
                        size="sm"
                    >
                        See More
                    </Button>
                </div>
            </Card.Header>
            <Card.Body>
                <div className="overflow-x-auto">
                    <table className="admin-table table align-middle">
                        <thead>
                            <tr>
                            {headers.map((header) =>(
                                <th key = {header}>
                                    {header}
                                </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                        {Userdata.length === 0 && (
                            <tr>
                                <td className="text-muted py-4" colSpan={Math.max(headers.length, 1)}>
                                    No page visit data is available yet.
                                </td>
                            </tr>
                        )}
                        {Userdata.map((item, index) =>(
                            <tr key={index}>
                                {headers.map((header)=>(
                                    <td key={`${index}-${header}`}>
                                    {item[header]}
                                    </td>
                                ))}
                            </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </Card.Body>
        </Card>
    );
}
