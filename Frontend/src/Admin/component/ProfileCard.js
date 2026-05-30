import {Card, Image, Button} from 'react-bootstrap';
import ProfilePicture from '../assets/img/team-1-800x800.jpg';
import { Icon } from '@iconify/react';

export default function ProfileCard() {
    return (
        <Card className="admin-card">
            <div className="flex flex-wrap justify-center">
                <div className="px-4 -mt-16">
                    <Image src={ProfilePicture} alt="John Smith" className="admin-profile-photo" />
                </div>
                <div className="w-full flex justify-center py-4 lg:pt-4 pt-8">
                    <div className="p-4 text-center">
                        <span className="text-xl font-medium block uppercase tracking-wide text-gray-900">
                            22
                        </span>
                        <span className="text-sm text-gray-700">Friends</span>
                    </div>
                    <div className="p-4 text-center">
                        <span className="text-xl font-medium block uppercase tracking-wide text-gray-900">
                            89
                        </span>
                        <span className="text-sm text-gray-700">Comments</span>
                    </div>
                    <div className="p-4 text-center">
                        <span className="text-xl font-medium block uppercase tracking-wide text-gray-900">
                            10
                        </span>
                        <span className="text-sm text-gray-700">Photos</span>
                    </div>
                </div>
            </div>
            <div className="text-center">
                <h5 className="admin-card-title">John Smith</h5>
                <div className="mt-0 mb-2 text-gray-700 flex items-center justify-center gap-2">
                    <Icon icon="mdi:place" width="20" />
                    Los Angeles, California
                </div>
                <div className="mb-2 text-gray-700 mt-10 flex items-center justify-center gap-2">
                    <Icon icon="marketeq:work" width="20" />
                    Solution Manager - Creative Tim Officer
                </div>
                <div className="mb-2 text-gray-700 flex items-center justify-center gap-2">
                    <Icon icon="codicon:account" width="20" />
                    University of Computer Science
                </div>
            </div>
            <Card.Body>
                <div className="border-t border-lightBlue-200 text-center px-2 ">
                    <p color="blueGray">
                        An artist of considerable range, Jenna the name taken by
                        Melbourne-raised, Brooklyn-based Nick Murphy writes,
                        performs and records all of his own music, giving it a
                        warm, intimate feel with a solid groove structure. An
                        artist of considerable range.
                    </p>
                </div>
            </Card.Body>
            <Card.Footer>
                <div className="w-full flex justify-center -mt-8">
                    <a
                        href="#pablo"
                        className="mt-5"
                        onClick={(e) => e.preventDefault()}
                    >
                        <Button variant="outline-primary" size="sm">
                            Show more
                        </Button>
                    </a>
                </div>
            </Card.Footer>
        </Card>
    );
}
