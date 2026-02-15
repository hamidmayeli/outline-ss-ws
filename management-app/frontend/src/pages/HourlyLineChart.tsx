import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  LineChart as RechartsLineChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  CartesianGrid,
  Brush,
} from 'recharts';
import { useTimeZone } from '../contexts/TimeZoneContext';
import { api } from '../services/api';
import { formatBytes } from '../utils/formatBytes';
import './HourlyLineChart.css';

interface ChartPoint {
  timestamp: number;
  label: string;
  bytes: number;
}

interface UserSeries {
  key: string;
  name: string;
  color: string;
}

type PerUserPoint = {
  timestamp: number;
  label: string;
  [key: string]: number | string;
};

const LINE_COLORS = [
  '#4285F4', '#EA4335', '#FBBC04', '#34A853', '#FF6D00',
  '#46BDC6', '#7B1FA2', '#E91E63', '#00897B', '#C0CA33',
  '#F4511E', '#3949AB', '#00ACC1', '#7CB342', '#FB8C00'
];

export const HourlyLineChart: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [hours, setHours] = useState(24);
  const [totalData, setTotalData] = useState<ChartPoint[]>([]);
  const [perUserData, setPerUserData] = useState<PerUserPoint[]>([]);
  const [userSeries, setUserSeries] = useState<UserSeries[]>([]);
  const [hiddenSeries, setHiddenSeries] = useState<Set<string>>(new Set());
  const { offsetMinutes, setOffsetMinutes, options } = useTimeZone();
  const offsetMs = offsetMinutes * 60 * 1000;

  const formatTimeLabel = useCallback((timestamp: number) => {
    const date = new Date(timestamp + offsetMs);
    const hour = date.getUTCHours();
    const minute = date.getUTCMinutes();
    if (hour === 0) {
      const month = (date.getUTCMonth() + 1).toString().padStart(2, '0');
      const day = date.getUTCDate().toString().padStart(2, '0');
      return `${month}/${day}`;
    }
    return `${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
  }, [offsetMs]);

  const formatTimeLabelTooltip = useCallback((timestamp: number) => {
    const date = new Date(timestamp + offsetMs);
    const hour = date.getUTCHours();
    const minute = date.getUTCMinutes();
    const month = (date.getUTCMonth() + 1).toString().padStart(2, '0');
    const day = date.getUTCDate().toString().padStart(2, '0');

    return `${month}/${day} ${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
  }, [offsetMs]);

  const loadUsage = useCallback(async () => {
    try {
      setError('');
      const usage = await api.getHourlyUsage(hours);
      const totals = new Map<number, ChartPoint>();
      const perUser = new Map<number, PerUserPoint>();
      const series = usage.map((client, index) => ({
        key: `client_${client.clientId}`,
        name: client.clientName,
        color: LINE_COLORS[index % LINE_COLORS.length],
      }));

      usage.forEach((client) => {
        const seriesKey = `client_${client.clientId}`;
        client.dataPoints.forEach((point) => {
          const date = new Date(point.timestamp);
          const timestamp = date.getTime();
          const label = formatTimeLabelTooltip(timestamp);

          const existingTotal = totals.get(timestamp);
          if (existingTotal) {
            existingTotal.bytes += point.bytesTransferred;
          } else {
            totals.set(timestamp, { timestamp, label, bytes: point.bytesTransferred });
          }

          const existingUser = perUser.get(timestamp) ?? { timestamp, label };
          const currentValue = typeof existingUser[seriesKey] === 'number' ? existingUser[seriesKey] as number : 0;
          existingUser[seriesKey] = currentValue + point.bytesTransferred;
          perUser.set(timestamp, existingUser);
        });
      });

      const totalSeries = Array.from(totals.values()).sort((a, b) => a.timestamp - b.timestamp);
      const perUserSeries = Array.from(perUser.values()).sort((a, b) => a.timestamp - b.timestamp);

      setTotalData(totalSeries);
      setPerUserData(perUserSeries);
      setUserSeries(series);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load hourly usage');
    } finally {
      setLoading(false);
    }
  }, [hours, formatTimeLabelTooltip]);

  useEffect(() => {
    loadUsage();
  }, [loadUsage]);

  const totalBytes = useMemo(
    () => totalData.reduce((sum, point) => sum + point.bytes, 0),
    [totalData]
  );

  const visibleUserSeries = useMemo(
    () => userSeries.filter((series) => !hiddenSeries.has(series.key)),
    [userSeries, hiddenSeries]
  );

  const toggleSeries = useCallback((key: string) => {
    setHiddenSeries((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }, []);

  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: ChartPoint }> }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="custom-tooltip">
          <p className="tooltip-name">{formatTimeLabelTooltip(data.timestamp)}</p>
          <p className="tooltip-value">{formatBytes(data.bytes)}</p>
        </div>
      );
    }
    return null;
  };

  const PerUserTooltip = ({
    active,
    payload,
  }: {
    active?: boolean;
    payload?: Array<{ name: string; value: number; color: string; payload?: PerUserPoint }>;
  }) => {
    if (active && payload && payload.length) {
      const timestamp = payload[0]?.payload?.timestamp;
      return (
        <div className="custom-tooltip">
          {typeof timestamp === 'number' && (
            <p className="tooltip-name">{formatTimeLabelTooltip(timestamp)}</p>
          )}
          {payload.map((entry) => (
            <p key={entry.name} className="tooltip-value" style={{ color: entry.color }}>
              {entry.name}: {formatBytes(entry.value)}
            </p>
          ))}
        </div>
      );
    }
    return null;
  };

  const renderPerUserLegend = () => (
    <div className="hourlychart-legend">
      {userSeries.map((series) => (
        <label key={series.key} className="hourlychart-legend-item">
          <input
            type="checkbox"
            checked={!hiddenSeries.has(series.key)}
            onChange={() => toggleSeries(series.key)}
          />
          <span className="hourlychart-legend-color" style={{ backgroundColor: series.color }} />
          <span className="hourlychart-legend-label">{series.name}</span>
        </label>
      ))}
    </div>
  );

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
          <label className="timezone-label">
            <select value={offsetMinutes} onChange={(event) => setOffsetMinutes(Number(event.target.value))}>
              {options.map((option) => (
                <option key={option.offsetMinutes} value={option.offsetMinutes}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label className="hours-label">
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

      {totalData.length === 0 ? (
        <div className="empty-state">
          <p>No usage data available</p>
        </div>
      ) : (
        <div className="hourlychart-section">
          <div className="hourlychart-content">
            <ResponsiveContainer width="100%" height={420}>
              <RechartsLineChart data={totalData} margin={{ top: 20, right: 24, left: 12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis
                  dataKey="timestamp"
                  tickFormatter={(value) => formatTimeLabel(Number(value))}
                />
                <YAxis tickFormatter={(value) => formatBytes(value)} width={90} />
                <Tooltip content={<CustomTooltip />} />
                <Brush
                  dataKey="timestamp"
                  height={22}
                  travellerWidth={12}
                  tickFormatter={(value) => formatTimeLabelTooltip(Number(value))}
                />
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

          <div className="hourlychart-subsection">
            <h2>Per User Usage</h2>
            <p className="hourlychart-subtitle">Hourly data usage by client</p>
          </div>
          <div className="hourlychart-content">
            {renderPerUserLegend()}
            <ResponsiveContainer width="100%" height={420}>
              <RechartsLineChart data={perUserData} margin={{ top: 20, right: 24, left: 12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis
                  dataKey="timestamp"
                  tickFormatter={(value) => formatTimeLabel(Number(value))}
                />
                <YAxis tickFormatter={(value) => formatBytes(value)} width={90} />
                <Tooltip content={<PerUserTooltip />} />
                <Brush
                  dataKey="timestamp"
                  height={22}
                  travellerWidth={12}
                  tickFormatter={(value) => formatTimeLabelTooltip(Number(value))}
                />
                {visibleUserSeries.map((series) => (
                  <Line
                    key={series.key}
                    type="monotone"
                    dataKey={series.key}
                    name={series.name}
                    stroke={series.color}
                    strokeWidth={2}
                    dot={false}
                  />
                ))}
              </RechartsLineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}
    </div>
  );
};
