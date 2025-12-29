import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import App from './App';

describe('App', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    window.location.hash = '';
  });

  it('renders header with ToskaMesh branding', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => [],
    } as Response);

    // Act
    render(<App />);

    // Assert
    expect(screen.getByText('ToskaMesh')).toBeInTheDocument();
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });

  it('renders navigation tabs', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => [],
    } as Response);

    // Act
    render(<App />);

    // Assert
    expect(screen.getByText('Services')).toBeInTheDocument();
    expect(screen.getByText('Traces')).toBeInTheDocument();
  });

  it('shows home page by default', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => [],
    } as Response);

    // Act
    render(<App />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Service Catalog')).toBeInTheDocument();
    });
  });

  it('shows service page when navigating to #/services/:name', async () => {
    // Arrange
    window.location.hash = '#/services/my-service';
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => [
        {
          serviceName: 'my-service',
          instances: [],
          health: [],
          metadata: null,
        },
      ],
    } as Response);

    // Act
    render(<App />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('my-service')).toBeInTheDocument();
    });
  });

  it('shows traces page when navigating to #/traces', async () => {
    // Arrange
    window.location.hash = '#/traces';
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => ({ items: [], total: 0, page: 1, pageSize: 50 }),
    } as Response);

    // Act
    render(<App />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Distributed Traces')).toBeInTheDocument();
    });
  });

  it('shows not found page for unknown routes', async () => {
    // Arrange
    window.location.hash = '#/unknown-route';
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      json: async () => [],
    } as Response);

    // Act
    render(<App />);

    // Assert
    expect(screen.getByText('Page Not Found')).toBeInTheDocument();
  });
});
