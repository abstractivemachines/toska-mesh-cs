import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingState } from './LoadingState';

describe('LoadingState', () => {
  it('renders default loading message', () => {
    render(<LoadingState />);
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders custom loading message', () => {
    render(<LoadingState message="Loading services..." />);
    expect(screen.getByText('Loading services...')).toBeInTheDocument();
  });

  it('renders spinner element', () => {
    const { container } = render(<LoadingState />);
    const spinner = container.querySelector('.loading-spinner');
    expect(spinner).toBeInTheDocument();
  });
});
