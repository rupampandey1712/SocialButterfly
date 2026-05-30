export default function footer() {
    return (
        <footer className="admin-footer py-5 px-4 px-lg-5 border-top flex flex-col lg:flex-row justify-between items-center">
            <p className="text-gray-700 mb-6 lg:mb-0">
                Copyright &copy; {new Date().getFullYear()}{' '}
                <a
                    href="/"
                    className="text-light-blue-500 hover:text-light-blue-700"
                >
                    Social Butterfly
                </a>
            </p>

            <ul className="list-unstyled flex">
                <li className="mr-6">
                    <a
                        className="text-gray-700 hover:text-gray-900 font-medium block text-sm"
                        href="/"
                    >
                        Overview
                    </a>
                </li>
                <li className="mr-6">
                    <a
                        className="text-gray-700 hover:text-gray-900 font-medium block text-sm"
                        href="/tables"
                    >
                        Users
                    </a>
                </li>
                <li className="mr-6">
                    <a
                        className="text-gray-700 hover:text-gray-900 font-medium block text-sm"
                        href="/calendar"
                    >
                        Calendar
                    </a>
                </li>
                <li>
                    <a
                        className="text-gray-700 hover:text-gray-900 font-medium block text-sm"
                        href="/settings"
                    >
                        Settings
                    </a>
                </li>
            </ul>
        </footer>
    );
}
