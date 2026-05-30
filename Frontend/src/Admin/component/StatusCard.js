import React from 'react';
import { Card } from 'react-bootstrap';
import { Icon } from '@iconify/react';

const iconAliases = {
    trending_up: 'tabler:trending-up',
    arrow_upward: 'mdi:arrow-upward',
    arrow_downward: 'mdi:arrow-downward',
    groups: 'material-symbols:groups',
    paid: 'material-symbols:paid',
    poll: 'ic:baseline-poll',
};

function getIcon(icon) {
    return iconAliases[icon] || icon;
}

function getColorClass(color = 'blue') {
    const normalized = color.replace('bg-', '').replace('-500', '');
    return `admin-icon-${normalized}`;
}

export default function StatusCard({ color, icon, title, amount, percentage, percentageColor, percentageIcon, date }) {
    return (
        <div className="px-4 mb-8">
            <Card className="admin-stat-card">
                <Card.Body>
                    <div className="admin-stat-top">
                        <div>
                            <p className="admin-stat-title">{title}</p>
                            <p className="admin-stat-amount">{amount}</p>
                        </div>
                        <span className={`admin-stat-icon ${getColorClass(color)}`}>
                            <Icon icon={getIcon(icon)} width="26" />
                        </span>
                    </div>
                    <div className="admin-stat-footer">
                        <span className={`admin-trend ${percentageColor}`}>
                            <Icon icon={getIcon(percentageIcon)} width="16" />
                            {percentage}%
                        </span>
                        <span>{date}</span>
                    </div>
                </Card.Body>
            </Card>
        </div>
    );
}
