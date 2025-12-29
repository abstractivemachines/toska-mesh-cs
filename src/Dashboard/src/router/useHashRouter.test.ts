import { describe, it, expect, beforeEach } from 'vitest';
import { createRoute } from './useHashRouter';

describe('createRoute', () => {
  it('creates route for simple path', () => {
    const route = createRoute('/');
    expect(route.path).toBe('/');
    expect('/'.match(route.pattern)).toBeTruthy();
  });

  it('creates route with named parameter', () => {
    const route = createRoute('/services/:name');

    const match = '/services/api-gateway'.match(route.pattern);
    expect(match).toBeTruthy();
    expect(match?.groups?.name).toBe('api-gateway');
  });

  it('creates route with multiple parameters', () => {
    const route = createRoute('/traces/:traceId/spans/:spanId');

    const match = '/traces/abc123/spans/span456'.match(route.pattern);
    expect(match).toBeTruthy();
    expect(match?.groups?.traceId).toBe('abc123');
    expect(match?.groups?.spanId).toBe('span456');
  });

  it('does not match incorrect paths', () => {
    const route = createRoute('/services/:name');

    expect('/traces/abc'.match(route.pattern)).toBeNull();
    expect('/services'.match(route.pattern)).toBeNull();
    expect('/services/'.match(route.pattern)).toBeNull();
  });
});
