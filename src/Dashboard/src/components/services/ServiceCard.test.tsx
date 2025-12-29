import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ServiceCard } from './ServiceCard';
import type { DashboardServiceCatalogItem } from '../../types/api';

describe('ServiceCard', () => {
  const mockService: DashboardServiceCatalogItem = {
    serviceName: 'api-gateway',
    instances: [
      {
        serviceName: 'api-gateway',
        serviceId: 'instance-1',
        address: '10.0.0.1',
        port: 8080,
        status: 'Healthy',
        metadata: {},
        registeredAt: '2024-01-01T00:00:00Z',
        lastHealthCheck: '2024-01-01T01:00:00Z',
      },
    ],
    health: [
      {
        serviceId: 'instance-1',
        serviceName: 'api-gateway',
        address: '10.0.0.1',
        port: 8080,
        status: 'Healthy',
        lastProbe: '2024-01-01T01:00:00Z',
        lastProbeType: 'http',
        message: null,
        metadata: {},
      },
    ],
    metadata: null,
  };

  beforeEach(() => {
    // Reset location hash
    window.location.hash = '';
  });

  it('renders service name', () => {
    render(<ServiceCard service={mockService} />);
    expect(screen.getByText('api-gateway')).toBeInTheDocument();
  });

  it('renders instance count', () => {
    render(<ServiceCard service={mockService} />);
    expect(screen.getByText(/1 instance\(s\)/)).toBeInTheDocument();
  });

  it('renders health snapshot count', () => {
    render(<ServiceCard service={mockService} />);
    expect(screen.getByText(/1 health snapshot\(s\)/)).toBeInTheDocument();
  });

  it('shows Healthy status badge when all instances are healthy', () => {
    render(<ServiceCard service={mockService} />);
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('shows Unhealthy status when any instance is unhealthy', () => {
    const unhealthyService: DashboardServiceCatalogItem = {
      ...mockService,
      health: [{ ...mockService.health[0], status: 'Unhealthy' }],
    };
    render(<ServiceCard service={unhealthyService} />);
    expect(screen.getByText('Unhealthy')).toBeInTheDocument();
  });

  it('navigates to service detail on click', () => {
    render(<ServiceCard service={mockService} />);
    fireEvent.click(screen.getByRole('button'));
    expect(window.location.hash).toBe('#/services/api-gateway');
  });

  it('navigates on Enter key press', () => {
    render(<ServiceCard service={mockService} />);
    fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' });
    expect(window.location.hash).toBe('#/services/api-gateway');
  });
});
