/**
 * Base API client with error handling and abort support
 */

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function buildUrl(baseUrl: string, path: string, params?: Record<string, string>): string {
  const trimmed = baseUrl.replace(/\/+$/, '');
  const base = trimmed ? `${trimmed}${path}` : path;

  if (!params || Object.keys(params).length === 0) {
    return base;
  }

  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.set(key, value);
    }
  }

  const queryString = searchParams.toString();
  return queryString ? `${base}?${queryString}` : base;
}

export async function apiGet<T>(
  baseUrl: string,
  path: string,
  params?: Record<string, string>,
  signal?: AbortSignal
): Promise<T> {
  const url = buildUrl(baseUrl, path, params);

  const response = await fetch(url, { signal });

  if (!response.ok) {
    throw new ApiError(response.status, `Request failed: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

export async function apiGetRaw(
  baseUrl: string,
  path: string,
  params?: Record<string, string>,
  signal?: AbortSignal
): Promise<{ content: string; contentType: string; status: number }> {
  const url = buildUrl(baseUrl, path, params);

  const response = await fetch(url, { signal });

  const content = await response.text();
  const contentType = response.headers.get('content-type') || 'application/json';

  return {
    content,
    contentType,
    status: response.status,
  };
}
