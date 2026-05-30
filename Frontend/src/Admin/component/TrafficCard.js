import {Card, Button, ProgressBar} from 'react-bootstrap';

export default function TrafficCard() {
    return (
        <Card className="admin-card">
            <Card.Header>
                <div className="w-full flex items-center justify-between">
                    <div>
                        <p className="admin-card-kicker">Acquisition</p>
                        <h2 className="admin-card-title">Social Media</h2>
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
                        <thead className="thead-light">
                            <tr>
                                <th>
                                    Referral
                                </th>
                                <th>
                                    Visitors
                                </th>
                                <th className="w-56">Share</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <th>
                                    Facebook
                                </th>
                                <td>
                                    1,480
                                </td>
                                <td>
                                    <ProgressBar className="admin-progress" now={60} />
                                </td>
                            </tr>
                            <tr>
                                <th>
                                    Google
                                </th>
                                <td>
                                    4,807
                                </td>
                                <td>
                                    <ProgressBar className="admin-progress" now={80} />
                                </td>
                            </tr>
                            <tr>
                                <th>
                                    Instagram
                                </th>
                                <td>
                                    3,678
                                </td>
                                <td>
                                    <ProgressBar className="admin-progress" now={75} />
                                </td>
                            </tr>
                            <tr>
                                <th>
                                    Twitter
                                </th>
                                <td>
                                    2,645
                                </td>
                                <td>
                                    <ProgressBar className="admin-progress" now={90} />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </Card.Body>
        </Card>
    );
}
