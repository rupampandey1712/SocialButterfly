import {Card, Button} from 'react-bootstrap';


export default function SettingsForm() {
    return (
        <Card className="admin-card">
            <Card.Header>
                <div className="w-full flex items-center justify-between">
                    <div>
                        <p className="admin-card-kicker">Profile</p>
                        <h2 className="admin-card-title">My Account</h2>
                    </div>
                    <Button
                        variant="outline-primary"
                        size="sm"
                    >
                        Settings
                    </Button>
                </div>
            </Card.Header>
            <Card.Body>
                <form>
                    <h6 className="admin-form-section-title">
                        User Information
                    </h6>
                    <div className="row g-3">
                        <div className="col-12 col-lg-6">
                            <input className="form-control"
                                type="text"
                                placeholder="Username"
                            />
                        </div>
                        <div className="col-12 col-lg-6">
                            <input className="form-control"
                                type="email"
                                placeholder="Email Address"
                            />
                        </div>
                        <div className="col-12 col-lg-6">
                            <input className="form-control"
                                type="text"
                                placeholder="First Name"
                            />
                        </div>
                        <div className="col-12 col-lg-6">
                            <input className="form-control"
                                type="text"
                                placeholder="Last Name"
                            />
                        </div>
                    </div>

                    <h6 className="admin-form-section-title">
                        Contact Information
                    </h6>
                    <div className="row g-3">
                        <div className="col-12">
                            <input className="form-control"
                                type="text"
                                placeholder="Address"
                            />
                        </div>
                        <div className="col-12 col-lg-4">
                            <input className="form-control"
                                type="text"
                                placeholder="City"
                            />
                        </div>
                        <div className="col-12 col-lg-4">
                            <input className="form-control"
                                type="text"
                                placeholder="Country"
                            />
                        </div>
                        <div className="col-12 col-lg-4">
                            <input className="form-control"
                                type="text"
                                placeholder="Postal Code"
                            />
                        </div>
                    </div>

                    <h6 className="admin-form-section-title">
                        About Me
                    </h6>
                    <div>
                        <textarea className="form-control" rows="5" placeholder="About Me" />
                    </div>
                </form>
            </Card.Body>
        </Card>
    );
}
