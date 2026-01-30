import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AreaChart as RechartsAreaChart,
  Area,
  ResponsiveContainer,
  Legend,
  Tooltip,
  XAxis,
  YAxis,
  CartesianGrid,
  Brush,
} from 'recharts';
import { useAuth } from '../contexts/AuthContext';
import { api, ApiError } from '../services/api';
import { formatBytes } from '../utils/formatBytes';
import './WeeklyComparisonChart.css';

interface WeekSeries {
  key: string;
  name: string;
  color: string;
  weekStart: Date;
}

type WeekHourPoint = {
  hourIndex: number;
  label: string;
  [key: string]: number | string;
};

const LINE_COLORS = [
  '#4285F4', '#EA4335', '#FBBC04', '#34A853', '#FF6D00',
  '#46BDC6', '#7B1FA2', '#E91E63', '#00897B', '#C0CA33',
  '#F4511E', '#3949AB', '#00ACC1', '#7CB342', '#FB8C00'
];

const HOURS_LOOKBACK = 24 * 30;
const WEEK_HOURS = 24 * 7;
const HOUR_MS = 60 * 60 * 1000;
const WEEK_MS = WEEK_HOURS * HOUR_MS;
const DAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

const getWeekStart = (date: Date) => {
  const result = new Date(date);
  const dayIndex = (result.getDay() + 6) % 7;
  result.setHours(0, 0, 0, 0);
  result.setDate(result.getDate() - dayIndex);
  return result;
};

const formatHourLabel = (hourIndex: number) => {
  const dayIndex = Math.floor(hourIndex / 24);
  const hour = hourIndex % 24;
  return `${DAY_LABELS[dayIndex]} ${hour.toString().padStart(2, '0')}:00`;
};

const buildWeekLabel = (index: number) => {
  if (index === 0) return 'Current Week';
  if (index === 1) return 'Last Week';
  return `${index} Weeks Ago`;
};

export const WeeklyComparisonChart: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [weekSeries, setWeekSeries] = useState<WeekSeries[]>([]);
  const [chartData, setChartData] = useState<WeekHourPoint[]>([]);
  const { token, logout } = useAuth();

  const loadUsage = useCallback(async () => {
    if (!token) return;

    try {
      setError('');
      setLoading(true);
      const usage = await api.getHourlyUsage(token, HOURS_LOOKBACK);
      const now = new Date();
      const oldestTime = now.getTime() - HOURS_LOOKBACK * HOUR_MS;
      const currentWeekStart = getWeekStart(now);
      const weekStarts: Date[] = [];
      const cursor = new Date(currentWeekStart);

      while (cursor.getTime() + WEEK_MS > oldestTime) {
        weekStarts.push(new Date(cursor));
        cursor.setDate(cursor.getDate() - 7);
      }

      const series = weekStarts.map((start, index) => ({
        key: `week_${index}`,
        name: buildWeekLabel(index),
        color: LINE_COLORS[index % LINE_COLORS.length],
        weekStart: start,
      }));

      const data: WeekHourPoint[] = Array.from({ length: WEEK_HOURS }, (_, hourIndex) => {
        const base: WeekHourPoint = { hourIndex, label: formatHourLabel(hourIndex) };
        series.forEach((entry) => {
          base[entry.key] = 0;
        });
        return base;
      });

      usage.forEach((client) => {
        client.dataPoints.forEach((point) => {
          const timestamp = new Date(point.timestamp).getTime();
          if (Number.isNaN(timestamp) || timestamp < oldestTime) return;

          const weekStart = getWeekStart(new Date(timestamp));
          const offset = Math.floor((currentWeekStart.getTime() - weekStart.getTime()) / WEEK_MS);
          if (offset < 0 || offset >= series.length) return;

          const hourIndex = Math.floor((timestamp - weekStart.getTime()) / HOUR_MS);
          if (hourIndex < 0 || hourIndex >= WEEK_HOURS) return;

          const key = series[offset].key;
          const currentValue = typeof data[hourIndex][key] === 'number' ? data[hourIndex][key] as number : 0;
          data[hourIndex][key] = currentValue + point.bytesTransferred;
        });
      });

      setWeekSeries(series);
      setChartData(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout();
      } else {
        setError('Failed to load weekly comparison');
      }
    } finally {
      setLoading(false);
    }
  }, [token, logout]);

  useEffect(() => {
    loadUsage();
  }, [loadUsage]);

  const totalBytes = useMemo(() => {
    if (chartData.length === 0 || weekSeries.length === 0) return 0;
    return chartData.reduce((sum, point) => {
      const pointSum = weekSeries.reduce((weekSum, series) => {
        const value = point[series.key];
        return weekSum + (typeof value === 'number' ? value : 0);
      }, 0);
      return sum + pointSum;
    }, 0);
  }, [chartData, weekSeries]);

  const WeeklyTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ name: string; value: number; color: string }> }) => {
    if (active && payload && payload.length) {
      return (
        <div className="custom-tooltip">
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

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Loading report...</p>
      </div>
    );
  }

  return (
    <div className="weeklychart-container">
      <div className="weeklychart-header">
        <div>
          <h1>Weekly Comparison</h1>
          <p className="weeklychart-subtitle">
            Hourly usage from the last 30 days ({formatBytes(totalBytes)})
          </p>
        </div>
        <div className="weeklychart-controls">
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
        <div className="weeklychart-content">
          <ResponsiveContainer width="100%" height={480}>
            <RechartsAreaChart data={chartData} margin={{ top: 20, right: 24, left: 12, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="label" interval={11} />
              <YAxis tickFormatter={(value) => formatBytes(value)} width={90} />
              <Tooltip content={<WeeklyTooltip />} />
              <Legend />
              <Brush dataKey="label" height={22} travellerWidth={12} />
              {weekSeries.map((series) => (
                <Area
                  key={series.key}
                  type="monotone"
                  dataKey={series.key}
                  name={series.name}
                  stroke={series.color}
                  strokeWidth={2}
                  dot={false}
                  fill={series.color}
                  fillOpacity={0.18}
                />
              ))}
            </RechartsAreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
};
