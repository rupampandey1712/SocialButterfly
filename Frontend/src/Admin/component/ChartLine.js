import { useEffect, useRef } from 'react';
import Chart from 'chart.js/auto';
import {Card} from 'react-bootstrap';

export default function ChartLine() {
    const chartRef = useRef(null)
    useEffect(() => {
        if(chartRef.current){
            chartRef.current.destroy();
        }

        var ctx = document.getElementById('line-chart').getContext('2d');
        var primaryGradient = ctx.createLinearGradient(0, 0, 0, 360);
        primaryGradient.addColorStop(0, 'rgba(34, 211, 238, 0.28)');
        primaryGradient.addColorStop(1, 'rgba(34, 211, 238, 0)');

        var config = {
            type: 'line',
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
                        backgroundColor: primaryGradient,
                        borderColor: '#22d3ee',
                        data: [65, 78, 66, 44, 56, 67, 75],
                        fill: true,
                        borderWidth: 3,
                        pointBackgroundColor: '#ffffff',
                        pointBorderColor: '#22d3ee',
                        pointBorderWidth: 3,
                        pointRadius: 4,
                        tension: 0.42,
                    },
                    {
                        label: new Date().getFullYear() - 1,
                        fill: false,
                        backgroundColor: '#a78bfa',
                        borderColor: '#a78bfa',
                        data: [40, 68, 86, 74, 56, 60, 87],
                        borderWidth: 3,
                        pointBackgroundColor: '#ffffff',
                        pointBorderColor: '#a78bfa',
                        pointBorderWidth: 3,
                        pointRadius: 4,
                        tension: 0.42,
                    },
                ],
            },
            options: {
                maintainAspectRatio: false,
                responsive: true,
                plugins: {
                    legend: {
                        labels: {
                            color: '#dbeafe',
                            usePointStyle: true,
                            boxWidth: 8,
                        },
                        align: 'end',
                        position: 'bottom',
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
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
                <h2 className="admin-card-title">Sales value</h2>
            </Card.Header>
            <Card.Body>
                <div className="relative h-96">
                    <canvas id="line-chart"></canvas>
                </div>
            </Card.Body>
        </Card>
    );
}
