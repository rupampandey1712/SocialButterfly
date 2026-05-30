import StatusCard from '../component/StatusCard';
import ChartLine from '../component/ChartLine';
import ChartBar from '../component/ChartBar';
import PageVisitsCard from '../component/PageVisitsCard';
import TrafficCard from '../component/TrafficCard';
import { Icon } from '@iconify/react';

export default function Dashboard() {
    return (
        <>
            <div className="admin-dashboard-hero bg-light-blue-500 px-3 md:px-8">
                <div className="container mx-auto max-w-full">
                    <div className="admin-command-panel">
                        <div className="admin-command-main">
                            <span className="admin-command-badge">
                                <Icon icon="lucide:sparkles" width="16" />
                                AI command center
                            </span>
                            <h2>Monitor growth, trust, and community signals in one intelligent cockpit.</h2>
                            <p>
                                Your social network is trending positive. AI detected stronger creator activity,
                                stable moderation load, and a healthy rise in returning users.
                            </p>
                            <div className="admin-command-actions">
                                <a href="/tables" className="admin-primary-action">
                                    <Icon icon="lucide:users-round" width="17" />
                                    Review users
                                </a>
                                <a href="/calendar" className="admin-secondary-action">
                                    <Icon icon="lucide:calendar-days" width="17" />
                                    Plan content
                                </a>
                            </div>
                        </div>
                        <div className="admin-command-side">
                            <div className="admin-model-ring">
                                <div>
                                    <strong>84%</strong>
                                    <span>AI Score</span>
                                </div>
                            </div>
                            <div className="admin-ai-grid">
                                <div>
                                    <span>Sentiment</span>
                                    <strong>High</strong>
                                </div>
                                <div>
                                    <span>Risk</span>
                                    <strong>Low</strong>
                                </div>
                                <div>
                                    <span>Growth</span>
                                    <strong>+8.2%</strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div className="px-3 md:px-8 -mt-24">
                <div className="container mx-auto max-w-full">
                    <div className="grid grid-cols-1 xl:grid-cols-5">
                        <div className="xl:col-start-1 xl:col-end-4 px-4 mb-14">
                            <ChartLine />
                        </div>
                        <div className="xl:col-start-4 xl:col-end-6 px-4 mb-14">
                            <ChartBar />
                        </div>
                    </div>
                </div>
            </div>

            <div className="px-3 md:px-8">
                <div className="container mx-auto max-w-full">
                    <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-4 mb-4">
                        <StatusCard
                            color="bg-pink-500"
                            icon="tabler:trending-up"
                            title="Traffic"
                            amount="350,897"
                            percentage="3.48"
                            percentageIcon="mdi:arrow-upward"
                            percentageColor="green"
                            date="Since last month"
                        />
                        <StatusCard
                            color="bg-orange-500"
                            icon="material-symbols:groups"
                            title="New Users"
                            amount="2,356"
                            percentage="3.48"
                            percentageIcon="mdi:arrow-downward"
                            percentageColor="red"
                            date="Since last week"
                        />
                        <StatusCard
                            color="bg-purple-500"
                            icon="material-symbols:paid"
                            title="Sales"
                            amount="924"
                            percentage="1.10"
                            percentageIcon="mdi:arrow-downward"
                            percentageColor="orange"
                            date="Since yesterday"
                        />
                        <StatusCard
                            color="bg-blue-500"
                            icon="ic:baseline-poll"
                            title="Performance"
                            amount="49,65%"
                            percentage="12"
                            percentageIcon="mdi:arrow-upward"
                            percentageColor="green"
                            date="Since last month"
                        />
                    </div>
                </div>
            </div>

            <div className="px-3 md:px-8 h-auto">
                <div className="container mx-auto max-w-full">
                    <div className="grid grid-cols-1 xl:grid-cols-5">
                        <div className="xl:col-start-1 xl:col-end-4 px-4 mb-14">
                            <PageVisitsCard />
                        </div>
                        <div className="xl:col-start-4 xl:col-end-6 px-4 mb-14">
                            <TrafficCard />
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
}
