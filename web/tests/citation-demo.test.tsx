import React from 'react';
import { it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CitationDemo } from '@/components/CitationDemo';

it('shows authored provenance and lets a viewer compare rejection, relevance, and overload limits', () => {
  render(<CitationDemo />);
  expect(screen.getByText(/Authored fixture · no model calls/)).toBeTruthy();
  expect(screen.getByText('Declaration signature matched')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: /2. Fabricated overload/ }));
  expect(screen.getByRole('heading', { name: 'Removed' })).toBeTruthy();
  expect(screen.getByText('Citation Rejected')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: /4. Real but unrelated/ }));
  expect(screen.getByText(/No matching name in extracted member facts/)).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Retained with limited evidence' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: /5. Name only/ }));
  expect(screen.getByText('Symbol-name existence only')).toBeTruthy();
  expect(screen.queryByText('Declaration signature matched')).toBeNull();
});
