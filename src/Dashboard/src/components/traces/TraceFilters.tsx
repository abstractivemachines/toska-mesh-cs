import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useConfig } from '../../context';
import { getTraceServiceNames } from '../../api/traces';
import type { TraceQueryParameters } from '../../types/api';

interface TraceFiltersProps {
  onFilterChange: (params: TraceQueryParameters) => void;
}

function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedValue(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);

  return debouncedValue;
}

export function TraceFilters({ onFilterChange }: TraceFiltersProps) {
  const { gatewayBaseUrl } = useConfig();
  const [allServices, setAllServices] = useState<string[]>([]);
  const [serviceName, setServiceName] = useState('');
  const [status, setStatus] = useState('');
  const [minDuration, setMinDuration] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const suggestionsRef = useRef<HTMLUListElement>(null);

  // Fetch service names on mount
  useEffect(() => {
    const controller = new AbortController();
    getTraceServiceNames(gatewayBaseUrl, controller.signal)
      .then(setAllServices)
      .catch(() => {}); // Ignore errors
    return () => controller.abort();
  }, [gatewayBaseUrl]);

  // Debounce all filter values
  const debouncedServiceName = useDebounce(serviceName, 300);
  const debouncedStatus = useDebounce(status, 300);
  const debouncedMinDuration = useDebounce(minDuration, 300);

  // Track if this is the initial mount
  const isInitialMount = useRef(true);

  // Build filter params
  const buildParams = useCallback((): TraceQueryParameters => {
    const params: TraceQueryParameters = {};
    if (debouncedServiceName) params.serviceName = debouncedServiceName;
    if (debouncedStatus) params.status = debouncedStatus;
    if (debouncedMinDuration) params.minDurationMs = parseFloat(debouncedMinDuration);
    return params;
  }, [debouncedServiceName, debouncedStatus, debouncedMinDuration]);

  // Auto-apply when any debounced value changes
  useEffect(() => {
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }
    onFilterChange(buildParams());
  }, [debouncedServiceName, debouncedStatus, debouncedMinDuration, buildParams, onFilterChange]);

  // Filter suggestions locally
  const suggestions = useMemo(() => {
    if (!serviceName) return [];
    const lower = serviceName.toLowerCase();
    return allServices.filter(s => s.toLowerCase().includes(lower));
  }, [serviceName, allServices]);

  const handleServiceSelect = (name: string) => {
    setServiceName(name);
    setShowSuggestions(false);
  };

  const handleClear = () => {
    setServiceName('');
    setStatus('');
    setMinDuration('');
    // Trigger immediate update on clear
    onFilterChange({});
  };

  // Close suggestions when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        inputRef.current &&
        !inputRef.current.contains(e.target as Node) &&
        suggestionsRef.current &&
        !suggestionsRef.current.contains(e.target as Node)
      ) {
        setShowSuggestions(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className="trace-filters">
      <div className="filter-row">
        <div className="filter-field autocomplete-container">
          <label htmlFor="service-filter">Service</label>
          <input
            ref={inputRef}
            id="service-filter"
            type="text"
            value={serviceName}
            onChange={(e) => {
              setServiceName(e.target.value);
              setShowSuggestions(true);
            }}
            onFocus={() => setShowSuggestions(true)}
            placeholder="Type to search..."
            autoComplete="off"
          />
          {showSuggestions && suggestions.length > 0 && (
            <ul ref={suggestionsRef} className="autocomplete-suggestions">
              {suggestions.map((s) => (
                <li
                  key={s}
                  onClick={() => handleServiceSelect(s)}
                  className={s === serviceName ? 'selected' : ''}
                >
                  {s}
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="filter-field">
          <label htmlFor="status-filter">Status</label>
          <select
            id="status-filter"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">All Statuses</option>
            <option value="Ok">Ok</option>
            <option value="Error">Error</option>
            <option value="Unset">Unset</option>
          </select>
        </div>

        <div className="filter-field">
          <label htmlFor="duration-filter">Min Duration (ms)</label>
          <input
            id="duration-filter"
            type="number"
            value={minDuration}
            onChange={(e) => setMinDuration(e.target.value)}
            placeholder="0"
            min="0"
          />
        </div>

        <div className="filter-actions">
          <button onClick={handleClear} className="btn btn-secondary">
            Clear
          </button>
        </div>
      </div>
    </div>
  );
}
