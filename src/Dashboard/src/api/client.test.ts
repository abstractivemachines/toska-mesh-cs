import { describe, it, expect, vi, beforeEach } from 'vitest';
import { buildUrl, apiGet, ApiError } from './client';

describe('buildUrl', () => {
  it('builds URL with base and path', () => {
    const result = buildUrl('http://localhost:5000', '/api/services');
    expect(result).toBe('http://localhost:5000/api/services');
  });

  it('trims trailing slashes from base URL', () => {
    const result = buildUrl('http://localhost:5000/', '/api/services');
    expect(result).toBe('http://localhost:5000/api/services');
  });

  it('returns path only when base URL is empty', () => {
    const result = buildUrl('', '/api/services');
    expect(result).toBe('/api/services');
  });

  it('adds query parameters', () => {
    const result = buildUrl('http://localhost:5000', '/api/query', { query: 'up', time: '123' });
    expect(result).toBe('http://localhost:5000/api/query?query=up&time=123');
  });

  it('ignores empty query parameters', () => {
    const result = buildUrl('http://localhost:5000', '/api/query', { query: 'up', empty: '' });
    expect(result).toBe('http://localhost:5000/api/query?query=up');
  });
});

describe('apiGet', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('fetches and returns JSON data', async () => {
    // Arrange
    const mockData = [{ name: 'service1' }];
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => mockData,
    } as Response);

    // Act
    const result = await apiGet<typeof mockData>('http://localhost:5000', '/api/services');

    // Assert
    expect(result).toEqual(mockData);
    expect(fetch).toHaveBeenCalledWith('http://localhost:5000/api/services', { signal: undefined });
  });

  it('throws ApiError on non-OK response', async () => {
    // Arrange
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
    } as Response);

    // Act & Assert
    await expect(apiGet('http://localhost:5000', '/api/services')).rejects.toThrow(ApiError);
  });

  it('passes abort signal to fetch', async () => {
    // Arrange
    const controller = new AbortController();
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({}),
    } as Response);

    // Act
    await apiGet('http://localhost:5000', '/api/services', undefined, controller.signal);

    // Assert
    expect(fetch).toHaveBeenCalledWith('http://localhost:5000/api/services', { signal: controller.signal });
  });
});
