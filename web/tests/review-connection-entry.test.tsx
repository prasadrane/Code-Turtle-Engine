import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ReviewEntry } from '@/components/ReviewEntry';
import HomePage from '@/app/page';

const live = vi.hoisted(() => ({ connectionError: 'Live reviews are unavailable here. Explore the sample or set up Code Turtle locally.',
  apiBase: null as string | null, error: null, rejection: null, isCompleted: false,
  reset: vi.fn(), startReview: vi.fn(), clearRejection: vi.fn() }));
vi.mock('@/hooks/useReviewStream', () => ({ useReviewStream: () => live }));

const props = () => ({ onSample: vi.fn(), onSubmit: vi.fn(), accessToken: '', setAccessToken: vi.fn(),
  initialUrl: 'https://github.com/fixture/repo/pull/1' });
afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe('Live review connection entry', () => {
  it('explains unavailable setup before submitting and hides the access-key prompt', () => {
    const callbacks = props();
    render(<ReviewEntry {...callbacks} connectionError={live.connectionError} />);
    const input = screen.getByRole('textbox', { name: 'GitHub Pull Request URL' }) as HTMLInputElement;
    const submit = screen.getByRole('button', { name: /start review/i }) as HTMLButtonElement;
    expect(screen.getByRole('status').textContent).toContain('Local server setup needed');
    expect(input.disabled).toBe(true);
    expect(submit.disabled).toBe(true);
    expect(input.getAttribute('aria-describedby')).toBe('connection-help');
    expect(screen.queryByLabelText('Local beta access key')).toBeNull();
    expect(screen.getByRole('link', { name: /set up local reviews/i }).getAttribute('href')).toContain('docs/beta/BETA.md#run-the-local-web-ui');
    fireEvent.submit(input.closest('form')!);
    expect(callbacks.onSubmit).not.toHaveBeenCalled();
  });

  it('keeps the sample available without a live connection', () => {
    const callbacks = props();
    render(<ReviewEntry {...callbacks} connectionError={live.connectionError} />);
    const sample = screen.getByRole('button', { name: /explore sample review/i }) as HTMLButtonElement;
    expect(sample.disabled).toBe(false);
    fireEvent.click(sample);
    expect(callbacks.onSample).toHaveBeenCalledOnce();
  });

  it('allows configured live review and keeps connection help beside the access key', () => {
    const callbacks = props();
    render(<ReviewEntry {...callbacks} connectionError={null} />);
    const input = screen.getByRole('textbox', { name: 'GitHub Pull Request URL' }) as HTMLInputElement;
    expect(input.disabled).toBe(false);
    expect(screen.getByLabelText('Local beta access key')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /start review/i }));
    expect(callbacks.onSubmit).toHaveBeenCalledWith(callbacks.initialUrl);
    expect(screen.getByText(/same hostname/i)).toBeTruthy();
    expect(screen.queryByText(/NEXT_PUBLIC|localhost:5000/)).toBeNull();
  });

  it('passes the connection state to the entry while allowing the sample to complete', async () => {
    render(<HomePage />);
    expect((screen.getByRole('button', { name: /start review/i }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByLabelText('Local beta access key')).toBeNull();
    expect(live.startReview).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /explore sample review/i }));
    expect(screen.getByText(/Illustrative sample · authored findings/i)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /skip to end/i }));
    await waitFor(() => expect(screen.getByTestId('final-report-view')).toBeTruthy());
    expect(live.startReview).not.toHaveBeenCalled();
  });
});
