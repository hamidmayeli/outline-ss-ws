import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

const STORAGE_KEY = 'reportTimeZoneOffsetMinutes';
const MIN_OFFSET_MINUTES = -12 * 60;
const MAX_OFFSET_MINUTES = 14 * 60;
const OFFSET_STEP_MINUTES = 15;

export interface TimeZoneOption {
  offsetMinutes: number;
  label: string;
}

const formatOffsetLabel = (offsetMinutes: number) => {
  const sign = offsetMinutes >= 0 ? '+' : '-';
  const absMinutes = Math.abs(offsetMinutes);
  const hours = Math.floor(absMinutes / 60);
  const minutes = absMinutes % 60;
  return `UTC${sign}${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
};

const buildOffsetOptions = () => {
  const options: TimeZoneOption[] = [];
  for (let offset = MIN_OFFSET_MINUTES; offset <= MAX_OFFSET_MINUTES; offset += OFFSET_STEP_MINUTES) {
    options.push({ offsetMinutes: offset, label: formatOffsetLabel(offset) });
  }
  return options;
};

interface TimeZoneContextType {
  offsetMinutes: number;
  setOffsetMinutes: (offsetMinutes: number) => void;
  options: TimeZoneOption[];
}

const TimeZoneContext = createContext<TimeZoneContextType | undefined>(undefined);

export const TimeZoneProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [offsetMinutes, setOffsetMinutes] = useState<number>(() => {
    const saved = localStorage.getItem(STORAGE_KEY);
    const savedValue = saved !== null ? Number(saved) : Number.NaN;
    if (!Number.isNaN(savedValue)) {
      return savedValue;
    }
    return -new Date().getTimezoneOffset();
  });

  const options = useMemo(() => buildOffsetOptions(), []);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, String(offsetMinutes));
  }, [offsetMinutes]);

  return (
    <TimeZoneContext.Provider value={{ offsetMinutes, setOffsetMinutes, options }}>
      {children}
    </TimeZoneContext.Provider>
  );
};

// eslint-disable-next-line react-refresh/only-export-components
export const useTimeZone = () => {
  const context = useContext(TimeZoneContext);
  if (!context) {
    throw new Error('useTimeZone must be used within TimeZoneProvider');
  }
  return context;
};
