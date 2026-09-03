import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import Pagination from './Pagination';

afterEach(cleanup);

describe('Pagination', () => {
  it('keeps the active page visible in a long page range', () => {
    render(<Pagination currentPage={5} totalPages={8} onPageChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: '5' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: '8' })).toBeInTheDocument();
  });

  it('moves to an adjacent page and disables the first-page previous action', () => {
    const onPageChange = vi.fn();
    render(<Pagination currentPage={1} totalPages={3} onPageChange={onPageChange} />);

    expect(screen.getByRole('button', { name: 'Trang trước' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '2' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});
