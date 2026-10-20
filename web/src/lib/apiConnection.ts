export interface ApiConnection { apiBase: string | null; connectionError: string | null; }
const missingConfiguration = 'This website has no live review server configured. Explore the sample, or run Code Turtle locally to review your pull requests.';
const invalidConfiguration = 'The review server address is invalid. Check the local setup guide for a valid HTTP(S) address.';
const localConnection = 'A local review server requires the frontend and backend to use the same local hostname and scheme. Run both services locally; the public website can explore the sample.';
const isLoopback = (hostname: string) => hostname === 'localhost' || hostname.endsWith('.localhost')
  || hostname === '[::1]' || /^127(?:\.\d{1,3}){3}$/.test(hostname);

/** Read only after hydration or immediately before a user-triggered request. */
export function browserFrontendOrigin(): string | null {
  return typeof window === 'undefined' ? null : window.location.origin;
}

export function resolveApiConnection(configured: string | undefined, production: boolean, frontendOrigin: string | null = null): ApiConnection {
  const value = configured?.trim() || (production ? '' : 'http://localhost:5000');
  if (!value) return { apiBase: null, connectionError: missingConfiguration };
  try {
    const api = new URL(value);
    if (!['http:', 'https:'].includes(api.protocol) || !api.hostname || api.username || api.password
      || api.href.includes('?') || api.href.includes('#')) throw new Error('Invalid API URL');
    const frontend = frontendOrigin ? new URL(frontendOrigin) : null;
    if (frontend && isLoopback(api.hostname)) {
      if (!isLoopback(frontend.hostname) || frontend.hostname !== api.hostname || frontend.protocol !== api.protocol)
        return { apiBase: null, connectionError: localConnection };
    }
    if (frontend?.protocol === 'https:' && api.protocol === 'http:')
      return { apiBase: null, connectionError: 'This HTTPS page cannot connect to an HTTP review server. Use an HTTPS server address, or run Code Turtle locally with a matching HTTP/HTTPS scheme.' };
    return { apiBase: api.href.replace(/\/+$/, ''), connectionError: null };
  } catch {
    // Configuration may contain secrets; never reflect its value or a URL parser error.
    return { apiBase: null, connectionError: invalidConfiguration };
  }
}

export function reviewConnectionMessage(error: unknown, fallback: string): string {
  return error instanceof TypeError
    ? 'Cannot reach the review server. Check that your local server is running, the app uses the correct server address, and the server allows this browser origin. Local services must use the same hostname and HTTP/HTTPS scheme. Network or browser restrictions can also block the connection.'
    : error instanceof Error ? error.message : fallback;
}
