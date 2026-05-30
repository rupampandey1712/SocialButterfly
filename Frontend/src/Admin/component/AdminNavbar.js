import { useLocation } from 'react-router-dom';
import { Button, Dropdown, Image } from 'react-bootstrap';
import { Icon } from '@iconify/react';
import ProfilePicture from '../assets/img/team-1-800x800.jpg';


export default function AdminNavbar({ showSidebar, setShowSidebar }) {
    const location = useLocation().pathname;
    const title = location === '/'
        ? 'Dashboard'
        : location.replace('/', '').replace(/-/g, ' ');

    return (
        <nav className="admin-navbar bg-light-blue-500 md:ml-64 py-6 px-3">
            <div className="container max-w-full mx-auto flex items-center justify-between md:pr-8 md:pl-10">
                <div className="md:hidden">
                    <Button
                        variant="light"
                        className="border-0 shadow-sm"
                        onClick={() => setShowSidebar('left-0')}
                        aria-label="Open sidebar"
                    >
                        <Icon icon="lucide:menu" width="22" />
                    </Button>
                    <div
                        className={`absolute top-2 md:hidden ${
                            showSidebar === 'left-0' ? 'left-64' : '-left-64'
                        } z-50 transition-all duration-300`}
                    >
                        <Button
                            variant="link"
                            className="text-white"
                            onClick={() => setShowSidebar('-left-64')}
                            aria-label="Close sidebar"
                        >
                            <Icon icon="lucide:x" width="24" />
                        </Button>
                    </div>
                </div>

                <div className="flex justify-between items-center w-full gap-3">
                    <div>
                        <p className="admin-page-kicker">Admin Console</p>
                        <h1 className="admin-page-title capitalize">{title}</h1>
                    </div>

                    <div className="admin-header-actions">
                        <div className="admin-search hidden sm:block">
                            <Icon icon="lucide:search" width="18" />
                            <input placeholder="Ask AI, search users..." aria-label="Search" />
                        </div>
                        <Button variant="link" className="admin-command-button hidden lg:inline-flex">
                            <Icon icon="lucide:wand-sparkles" width="17" />
                            AI Assist
                        </Button>
                        <Button variant="link" className="admin-icon-button hidden sm:inline-flex" aria-label="Notifications">
                            <Icon icon="lucide:bell" width="18" />
                        </Button>

                        <Dropdown align="end">
                            <Dropdown.Toggle variant="link" className="p-0 border-0 shadow-none">
                                <Image src={ProfilePicture} alt="Admin profile" className="admin-avatar" />
                            </Dropdown.Toggle>
                            <Dropdown.Menu>
                                <Dropdown.Item>Profile</Dropdown.Item>
                                <Dropdown.Item>Account settings</Dropdown.Item>
                                <Dropdown.Divider />
                                <Dropdown.Item>Sign out</Dropdown.Item>
                            </Dropdown.Menu>
                        </Dropdown>
                    </div>
                </div>
            </div>
        </nav>
    );
}
