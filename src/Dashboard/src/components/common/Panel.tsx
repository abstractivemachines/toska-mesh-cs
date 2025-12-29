import type { ReactNode } from 'react';

interface PanelProps {
  children: ReactNode;
  title?: string;
  className?: string;
}

export function Panel({ children, title, className = '' }: PanelProps) {
  return (
    <section className={`panel ${className}`.trim()}>
      {title && <h2>{title}</h2>}
      {children}
    </section>
  );
}
