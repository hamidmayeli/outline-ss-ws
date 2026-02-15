import React, { useState, useEffect, useCallback } from 'react';
import { PieChart as RechartsPieChart, Pie, ResponsiveContainer, Tooltip } from 'recharts';
import { useAuth } from '../contexts/AuthContext';
import { api, ApiError } from '../services/api';
import { formatBytes } from '../utils/formatBytes';
import './PieChart.css';

interface ChartData {
  name: string;
  value: number;
  percentage: number;
  fill: string;
}

const COLORS = [
  '#4285F4', '#EA4335', '#FBBC04', '#34A853', '#FF6D00',
  '#46BDC6', '#7B1FA2', '#E91E63', '#00897B', '#C0CA33',
  '#F4511E', '#3949AB', '#00ACC1', '#7CB342', '#FB8C00'
];

export const PieChart: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [chartData, setChartData] = useState<ChartData[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const { token, logout } = useAuth();

  const loadClients = useCallback(async (isRefresh = false) => {
    if (!token) return;
    
    try {
      if (isRefresh) {
        setRefreshing(true);
      }
      setError('');
      const data = await api.getClients();
      
      // Calculate chart data
      const clientsWithUsage = data.filter(client => {
        const usage = client.usageLast30Days;
        return usage && (usage.bytesUploaded + usage.bytesDownloaded) > 0;
      });

      const totalUsage = clientsWithUsage.reduce((sum, client) => {
        const usage = client.usageLast30Days!;
        return sum + usage.bytesUploaded + usage.bytesDownloaded;
      }, 0);

      const chartData: ChartData[] = clientsWithUsage.map((client, index) => {
        const usage = client.usageLast30Days!;
        const value = usage.bytesUploaded + usage.bytesDownloaded;
        return {
          name: client.name,
          value,
          percentage: (value / totalUsage) * 100,
          fill: COLORS[index % COLORS.length]
        };
      }).sort((a, b) => b.value - a.value);

      setChartData(chartData);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout();
      } else {
        setError('Failed to load clients');
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [token, logout]);

  useEffect(() => {
    loadClients();
  }, [loadClients]);

  const handleRefresh = () => {
    loadClients(true);
  };

  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: ChartData }> }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="custom-tooltip">
          <p className="tooltip-name">{data.name}</p>
          <p className="tooltip-value">{formatBytes(data.value)}</p>
          <p className="tooltip-percentage">{data.percentage.toFixed(1)}%</p>
        </div>
      );
    }
    return null;
  };

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Loading chart...</p>
      </div>
    );
  }

  return (
    <div className="piechart-container">
      <div className="piechart-header">
        <div>
          <h1>Client Usage Distribution</h1>
          <p className="piechart-subtitle">Data usage by client (last 30 days)</p>
        </div>
        <button 
          onClick={handleRefresh} 
          disabled={loading || refreshing}
          className="btn-secondary"
          title="Refresh data"
        >
          {refreshing ? '↻ Refreshing...' : '⟳ Refresh'}
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {chartData.length === 0 ? (
        <div className="empty-state">
          <p>No usage data available</p>
        </div>
      ) : (
        <div className="piechart-content">
          <div className="chart-wrapper">
            <ResponsiveContainer width="100%" height={400}>
              <RechartsPieChart>
                <Pie
                  data={chartData}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  outerRadius={120}
                  dataKey="value"
                  nameKey="name"
                />
                <Tooltip content={<CustomTooltip />} />
              </RechartsPieChart>
            </ResponsiveContainer>
          </div>
          <div className="piechart-legend-table">
            <table>
              <thead>
                <tr>
                  <th>Client</th>
                  <th>Usage</th>
                  <th>Share</th>
                </tr>
              </thead>
              <tbody>
                {chartData.map((entry) => (
                  <tr key={entry.name} style={{ color: entry.fill }}>
                    <td>{entry.name}</td>
                    <td>{formatBytes(entry.value)}</td>
                    <td>{entry.percentage.toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};

