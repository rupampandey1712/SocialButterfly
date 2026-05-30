import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import AdminNavbar from '../component/AdminNavbar';
import { Icon } from '@iconify/react';
import AppLogo from '../../components/logo/AppLogo';

const navItems = [
    { to: '/', label: 'Dashboard', icon: 'material-symbols:dashboard-outline-rounded' },
    { to: '/settings', label: 'Settings', icon: 'material-symbols:settings-outline-rounded' },
    { to: '/tables', label: 'Users', icon: 'material-symbols:table-chart-outline-rounded' },
    { to: '/maps', label: 'Maps', icon: 'material-symbols:map-outline-rounded' },
    { to: '/calendar', label: 'Calendar', icon: 'material-symbols:calendar-month-outline-rounded' },
];



export const Sidenav=()=> {
    const [showSidebar, setShowSidebar] = useState('-left-64');
    return (
        <>
            <AdminNavbar
                showSidebar={showSidebar}
                setShowSidebar={setShowSidebar}
            />
            <div
                className={`admin-sidebar h-screen fixed top-0 md:left-0 ${showSidebar} overflow-y-auto flex-row flex-nowrap overflow-hidden w-64 z-10 py-4 px-5 transition-all duration-300`}
            >
                <div className="flex-col items-stretch min-h-full flex-nowrap px-0 relative">
                    <NavLink to="/" className="admin-brand">
                        <AppLogo compact light showTagline />
                    </NavLink>
                    <div className="flex flex-col">
                        <hr className="my-4 min-w-full" />

                        <ul className="admin-nav-list">
                            {navItems.map((item) => (
                                <li key={item.to}>
                                    <NavLink
                                        to={item.to}
                                        className={({ isActive }) =>
                                            `admin-nav-link${isActive ? ' active' : ''}`
                                        }
                                    >
                                        <Icon icon={item.icon} width="21" />
                                        {item.label}
                                    </NavLink>
                                </li>
                            ))}
                        </ul>

                        <ul className="admin-nav-list absolute bottom-0 left-0 right-0">
                            <li>
                                <a
                                    href="https://material-tailwind.com/documentation/quick-start"
                                    target="_blank"
                                    rel="noreferrer"
                                    className="admin-nav-link"
                                >
                                    <Icon icon="material-symbols:help-outline-rounded" width="21" />
                                    Help Center
                                </a>
                            </li>
                            <li>
                                <a
                                    href="https://www.creative-tim.com/product/material-tailwind-dashboard-react"
                                    target="_blank"
                                    rel="noreferrer"
                                    className="admin-nav-link"
                                >
                                    <Icon icon="material-symbols:logout-rounded" width="21" />
                                    Sign out
                                </a>
                            </li>
                        </ul>
                    </div>
                </div>
            </div>
        </>
    );
}
