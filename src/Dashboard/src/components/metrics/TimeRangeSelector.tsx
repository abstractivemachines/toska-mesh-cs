import { useTimeRange, TIME_RANGE_PRESETS, type TimeRangePreset } from '../../context';

export function TimeRangeSelector() {
  const { range, setRange, presets } = useTimeRange();

  const handleSelect = (preset: TimeRangePreset) => {
    const now = Math.floor(Date.now() / 1000);
    setRange({
      label: preset.label,
      start: now - preset.duration,
      end: now,
      step: preset.step,
    });
  };

  return (
    <div className="time-range-selector">
      {presets.map((preset) => (
        <button
          key={preset.label}
          className={`time-range-btn ${range.label === preset.label ? 'active' : ''}`}
          onClick={() => handleSelect(preset)}
        >
          {preset.label}
        </button>
      ))}
    </div>
  );
}
