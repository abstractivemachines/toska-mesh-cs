import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusBadge } from './StatusBadge';

describe('StatusBadge', () => {
  it('renders the status text', () => {
    render(<StatusBadge status="Healthy" />);
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('applies healthy status class', () => {
    render(<StatusBadge status="Healthy" />);
    const badge = screen.getByText('Healthy');
    expect(badge).toHaveClass('status-healthy');
  });

  it('applies unhealthy status class', () => {
    render(<StatusBadge status="Unhealthy" />);
    const badge = screen.getByText('Unhealthy');
    expect(badge).toHaveClass('status-unhealthy');
  });

  it('applies degraded status class', () => {
    render(<StatusBadge status="Degraded" />);
    const badge = screen.getByText('Degraded');
    expect(badge).toHaveClass('status-degraded');
  });

  it('applies unknown status class for unknown values', () => {
    render(<StatusBadge status="SomeOtherStatus" />);
    const badge = screen.getByText('SomeOtherStatus');
    expect(badge).toHaveClass('status-unknown');
  });

  it('applies small size class when specified', () => {
    render(<StatusBadge status="Healthy" size="small" />);
    const badge = screen.getByText('Healthy');
    expect(badge).toHaveClass('pill-small');
  });
});
