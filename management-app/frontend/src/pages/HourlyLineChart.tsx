import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  LineChart as RechartsLineChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  CartesianGrid,
} from 'recharts';
import { useAuth } from '../contexts/AuthContext';
import { api, ApiError } from '../services/api';
import { formatBytes } from '../utils/formatBytes';
import './HourlyLineChart.css';

interface ChartPoint {
  timestamp: number;
  label: string;
  bytes: number;
}

export const HourlyLineChart: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [hours, setHours] = useState(24);
  const [chartData, setChartData] = useState<ChartPoint[]>([]);
  const { token, logout } = useAuth();

  const loadUsage = useCallback(async () => {
    if (!token) return;

    try {
      setError('');
      const usage = await api.getHourlyUsage(token, hours);
      const totals = new Map<number, ChartPoint>();

      usage.forEach((client) => {
        client.dataPoints.forEach((point) => {
          const date = new Date(point.timestamp);
          const timestamp = date.getTime();
          const label = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

          const existing = totals.get(timestamp);
          if (existing) {
            existing.bytes += point.bytesTransferred;
          } else {
            totals.set(timestamp, { timestamp, label, bytes: point.bytesTransferred });
          }
        });
      });

      const data = Array.from(totals.values()).sort((a, b) => a.timestamp - b.timestamp);
      setChartData(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout();
      } else {
        setError('Failed to load hourly usage');
      }
    } finally {
      setLoading(false);
    }
  }, [token, logout, hours]);

  useEffect(() => {
    loadUsage();
  }, [loadUsage]);

  const totalBytes = useMemo(
    () => chartData.reduce((sum, point) => sum + point.bytes, 0),
    [chartData]
  );

  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: ChartPoint }> }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="custom-tooltip">
          <p className="tooltip-name">{data.label}</p>
          <p className="tooltip-value">{formatBytes(data.bytes)}</p>
        </div>
      );
    }
    return null;
  };

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Loading report...</p>
      </div>
    );
  }

  return (
    <div className="hourlychart-container">
      <div className="hourlychart-header">
        <div>
          <h1>Hourly Usage</h1>
          <p className="hourlychart-subtitle">
            Total data transferred in the last {hours} hours ({formatBytes(totalBytes)})
          </p>
        </div>
        <div className="hourlychart-controls">
          <label className="hours-label">
            Hours
            <select value={hours} onChange={(event) => setHours(Number(event.target.value))}>
              <option value={6}>6h</option>
              <option value={12}>12h</option>
              <option value={24}>24h</option>
              <option value={168}>7d</option>
              <option value={720}>30d</option>
            </select>
          </label>
          <button className="btn-secondary" onClick={loadUsage}>
            Refresh
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {chartData.length === 0 ? (
        <div className="empty-state">
          <p>No usage data available</p>
        </div>
      ) : (
        <div className="hourlychart-content">
          <ResponsiveContainer width="100%" height={420}>
            <RechartsLineChart data={chartData} margin={{ top: 20, right: 24, left: 12, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="label" />
              <YAxis tickFormatter={(value) => formatBytes(value)} width={90} />
              <Tooltip content={<CustomTooltip />} />
              <Line
                type="monotone"
                dataKey="bytes"
                stroke="#4285F4"
                strokeWidth={2}
                dot={false}
              />
            </RechartsLineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
};
