import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { ServiceList } from './ServiceList';
import { ConfigProvider } from '../../context';

// Wrap component with providers
function renderWithProviders(ui: React.ReactNode) {
  return render(<ConfigProvider>{ui}</ConfigProvider>);
}

describe('ServiceList', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('shows loading state initially', () => {
    // Arrange - fetch never resolves
    vi.mocked(fetch).mockImplementation(() => new Promise(() => {}));

    // Act
    renderWithProviders(<ServiceList />);

    // Assert
    expect(screen.getByText(/Loading services/i)).toBeInTheDocument();
  });

  it('displays services when API succeeds', async () => {
    // Arrange
    const mockServices = [
      {
        serviceName: 'auth-service',
        instances: [],
        health: [],
        metadata: null,
      },
      {
        serviceName: 'order-service',
        instances: [],
        health: [],
        metadata: null,
      },
    ];

    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => mockServices,
    } as Response);

    // Act
    renderWithProviders(<ServiceList />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('auth-service')).toBeInTheDocument();
      expect(screen.getByText('order-service')).toBeInTheDocument();
    });
  });

  it('displays error message when API fails', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
    } as Response);

    // Act
    renderWithProviders(<ServiceList />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/Unable to load services/i)).toBeInTheDocument();
    });
  });

  it('displays empty message when no services', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => [],
    } as Response);

    // Act
    renderWithProviders(<ServiceList />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/No services discovered yet/i)).toBeInTheDocument();
    });
  });
});
