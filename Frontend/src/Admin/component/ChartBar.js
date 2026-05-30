import { useEffect,useRef } from 'react';
import Chart from 'chart.js/auto';
import {Card} from 'react-bootstrap';

export default function ChartBar() {
    const chartRef = useRef(null)
    useEffect(() => {
        if(chartRef.current){
            chartRef.current.destroy();
        }
        let config = {
            type: 'bar',
            data: {
                labels: [
                    'January',
                    'February',
                    'March',
                    'April',
                    'May',
                    'June',
                    'July',
                ],
                datasets: [
                    {
                        label: new Date().getFullYear(),
                        backgroundColor: '#22d3ee',
                        borderColor: '#22d3ee',
                        data: [30, 78, 56, 34, 100, 45, 13],
                        fill: false,
                        barThickness: 10,
                        borderRadius: 8,
                        hoverBackgroundColor: '#06b6d4',
                    },
                    {
                        label: new Date().getFullYear() - 1,
                        fill: false,
                        backgroundColor: '#a78bfa',
                        borderColor: '#a78bfa',
                        data: [27, 68, 86, 74, 10, 4, 87],
                        barThickness: 10,
                        borderRadius: 8,
                        hoverBackgroundColor: '#6d28d9',
                    },
                ],
            },
            options: {
                maintainAspectRatio: false,
                responsive: true,
                plugins: {
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                    },
                    legend: {
                        labels: {
                            color: '#dbeafe',
                            usePointStyle: true,
                            boxWidth: 8,
                        },
                        align: 'end',
                        position: 'bottom',
                    },
                },
                scales: {
                    x: {
                        ticks: {
                            color: '#94a3b8',
                        },
                        grid: {
                            display: false,
                        },
                    },
                    y: {
                        ticks: {
                            color: '#94a3b8',
                        },
                        grid: {
                            color: 'rgba(148, 163, 184, 0.16)',
                            drawBorder: false,
                        },
                            },
                },
            },
        };
        let ctx = document.getElementById('bar-chart').getContext('2d');
        chartRef.current = new Chart(ctx, config);
    

    return()=>{
        if(chartRef.current){
            chartRef.current.destroy();
        }
    };
}, []);

    return (
        <Card className="admin-card">
            <Card.Header>
                <p className="admin-card-kicker">Overview</p>
                <h2 className="admin-card-title">Orders value</h2>
            </Card.Header>
            <Card.Body>
                <div className="relative h-96">
                    <canvas id="bar-chart"></canvas>
                </div>
            </Card.Body>
        </Card>
    );
}
