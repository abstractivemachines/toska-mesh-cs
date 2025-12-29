import { createContext, useContext, useState, type ReactNode } from 'react';

export interface TimeRange {
  label: string;
  start: number; // Unix timestamp in seconds
  end: number; // Unix timestamp in seconds
  step: string; // Prometheus step (e.g., "15s", "1m", "5m")
}

interface TimeRangeContextValue {
  range: TimeRange;
  setRange: (range: TimeRange) => void;
  presets: TimeRangePreset[];
}

export interface TimeRangePreset {
  label: string;
  duration: number; // Duration in seconds
  step: string;
}

export const TIME_RANGE_PRESETS: TimeRangePreset[] = [
  { label: 'Last 15m', duration: 15 * 60, step: '15s' },
  { label: 'Last 1h', duration: 60 * 60, step: '1m' },
  { label: 'Last 6h', duration: 6 * 60 * 60, step: '5m' },
  { label: 'Last 24h', duration: 24 * 60 * 60, step: '15m' },
];

function createTimeRange(preset: TimeRangePreset): TimeRange {
  const now = Math.floor(Date.now() / 1000);
  return {
    label: preset.label,
    start: now - preset.duration,
    end: now,
    step: preset.step,
  };
}

const TimeRangeContext = createContext<TimeRangeContextValue | null>(null);

export function TimeRangeProvider({ children }: { children: ReactNode }) {
  const [range, setRange] = useState<TimeRange>(() => createTimeRange(TIME_RANGE_PRESETS[0]));

  return (
    <TimeRangeContext.Provider
      value={{
        range,
        setRange,
        presets: TIME_RANGE_PRESETS,
      }}
    >
      {children}
    </TimeRangeContext.Provider>
  );
}

export function useTimeRange(): TimeRangeContextValue {
  const context = useContext(TimeRangeContext);
  if (!context) {
    throw new Error('useTimeRange must be used within a TimeRangeProvider');
  }
  return context;
}

export function useTimeRangeFromPreset(preset: TimeRangePreset): TimeRange {
  return createTimeRange(preset);
}
