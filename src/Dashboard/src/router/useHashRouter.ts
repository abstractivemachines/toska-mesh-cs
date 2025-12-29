import { useEffect, useState, useCallback } from 'react';

export interface Route {
  path: string;
  pattern: RegExp;
}

export interface RouteMatch {
  route: Route | null;
  params: Record<string, string>;
}

/**
 * Convert a path pattern like "/services/:name" to a regex
 */
function pathToRegex(path: string): RegExp {
  // First replace path params, then escape slashes
  const withParams = path.replace(/:([a-zA-Z][a-zA-Z0-9_]*)/g, '(?<$1>[^/]+)');
  const pattern = withParams.replace(/\//g, '\\/');
  return new RegExp(`^${pattern}$`);
}

/**
 * Create a route from a path pattern
 */
export function createRoute(path: string): Route {
  return {
    path,
    pattern: pathToRegex(path),
  };
}

/**
 * Match a hash path against a list of routes
 */
function matchRoute(hash: string, routes: Route[]): RouteMatch {
  const path = hash.startsWith('#') ? hash.slice(1) : hash;
  const normalizedPath = path || '/';

  for (const route of routes) {
    const match = normalizedPath.match(route.pattern);
    if (match) {
      return {
        route,
        params: match.groups || {},
      };
    }
  }

  return { route: null, params: {} };
}

/**
 * Hook for hash-based routing
 */
export function useHashRouter(routes: Route[]): RouteMatch & { navigate: (path: string) => void } {
  const [hash, setHash] = useState(() => window.location.hash || '#/');

  useEffect(() => {
    const handleHashChange = () => {
      setHash(window.location.hash || '#/');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  const navigate = useCallback((path: string) => {
    const newHash = path.startsWith('#') ? path : `#${path}`;
    window.location.hash = newHash;
  }, []);

  const match = matchRoute(hash, routes);

  return {
    ...match,
    navigate,
  };
}

/**
 * Navigate to a new hash path
 */
export function navigate(path: string): void {
  const newHash = path.startsWith('#') ? path : `#${path}`;
  window.location.hash = newHash;
}
