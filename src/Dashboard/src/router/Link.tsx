import { type ReactNode, type MouseEvent, useState, useEffect } from 'react';
import { navigate } from './useHashRouter';

interface LinkProps {
  to: string;
  children: ReactNode;
  className?: string;
  activeClassName?: string;
}

export function Link({ to, children, className = '', activeClassName = '' }: LinkProps) {
  const [currentHash, setCurrentHash] = useState(() => window.location.hash || '#/');

  useEffect(() => {
    const handleHashChange = () => {
      setCurrentHash(window.location.hash || '#/');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  const href = to.startsWith('#') ? to : `#${to}`;
  const normalizedTo = to.startsWith('/') ? `#${to}` : to;
  const isActive = currentHash === href || currentHash === normalizedTo;

  const handleClick = (e: MouseEvent<HTMLAnchorElement>) => {
    e.preventDefault();
    navigate(to);
  };

  const combinedClassName = [className, isActive ? activeClassName : ''].filter(Boolean).join(' ');

  return (
    <a href={href} onClick={handleClick} className={combinedClassName || undefined}>
      {children}
    </a>
  );
}
