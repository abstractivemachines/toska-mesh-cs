import { useEffect, useRef } from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';

interface SparklineProps {
  data: [number[], number[]]; // [timestamps, values]
  width?: number;
  height?: number;
  color?: string;
  fillOpacity?: number;
}

export function Sparkline({
  data,
  width = 200,
  height = 40,
  color = '#3b82f6',
  fillOpacity = 0.2,
}: SparklineProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<uPlot | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    // Clean up previous chart
    if (chartRef.current) {
      chartRef.current.destroy();
      chartRef.current = null;
    }

    // Don't render if no data
    if (data[0].length === 0) return;

    const fillColor = color + Math.round(fillOpacity * 255).toString(16).padStart(2, '0');

    const opts: uPlot.Options = {
      width,
      height,
      cursor: { show: false },
      legend: { show: false },
      scales: {
        x: { time: true },
        y: { auto: true },
      },
      axes: [
        { show: false },
        { show: false },
      ],
      series: [
        {},
        {
          stroke: color,
          width: 1.5,
          fill: fillColor,
        },
      ],
    };

    chartRef.current = new uPlot(opts, data, containerRef.current);

    return () => {
      if (chartRef.current) {
        chartRef.current.destroy();
        chartRef.current = null;
      }
    };
  }, [data, width, height, color, fillOpacity]);

  if (data[0].length === 0) {
    return (
      <div className="sparkline-empty" style={{ width, height }}>
        <span className="muted">No data</span>
      </div>
    );
  }

  return <div ref={containerRef} className="sparkline" />;
}
