import React from 'react';
import { Play, KeyRound, Server, ArrowUpRight } from 'lucide-react';
import { PrInput } from './PrInput';

export const LOCAL_SETUP_GUIDE = 'https://github.com/prasadrane/Code-Turtle-Engine/blob/main/docs/beta/BETA.md#run-the-local-web-ui';

export function ReviewEntry({ onSample, onSubmit, accessToken, setAccessToken, initialUrl, connectionError }: {
  onSample: () => void; onSubmit: (url: string) => void; accessToken: string;
  setAccessToken: (token: string) => void; initialUrl: string; connectionError?: string | null;
}) {
  return <div className="review-entry-grid">
    <aside className="sample-entry"><span className="entry-icon"><Play size={20} /></span><span className="eyebrow">EXPLORE AT YOUR OWN PACE</span>
      <h3>See a review in action.</h3><p>Explore an illustrative review with readable findings, source changes, and inline comments.</p>
      <button className="button button-green" onClick={onSample}>Explore sample review <ArrowUpRight size={16} /></button>
      <small>Authored sample findings. No sign-up, server, API key, or model calls.</small></aside>
    <div className="workspace-card live-entry"><div className="workspace-card-heading"><span className="entry-icon"><Server size={21} /></span><div><h3>Review with your local server.</h3><p>C# / .NET · Self-hosted beta</p></div></div>
      <p className="connection-prerequisite" id="connection-help" role={connectionError ? 'status' : undefined}>{connectionError
        ? <><strong>Local server setup needed.</strong> {connectionError}</>
        : 'Live reviews require a running trusted-local server, an access key or active session, and a repository on your server’s allowlist.'}</p>
      <p className="connection-prerequisite"><a className="text-link" href={LOCAL_SETUP_GUIDE}>Set up local reviews <ArrowUpRight size={14} /></a></p>
      <PrInput onSubmit={onSubmit} onTryDemo={onSample} showDemo={false} initialUrl={initialUrl} disabled={Boolean(connectionError)} descriptionId="connection-help" presets={connectionError ? [] : undefined} />
      {!connectionError && <details className="access-settings"><summary><KeyRound size={14} /> Connect your local beta server <span>Access key and connection help</span></summary>
        <div className="access-settings-body"><label htmlFor="access-key">Local beta access key</label>
          <input id="access-key" type="password" autoComplete="off" aria-describedby="access-help" value={accessToken} onChange={event => setAccessToken(event.target.value)} placeholder="Enter your server access key" />
          <p id="access-help">Use your local server’s key. It stays in page memory and is exchanged for a short-lived session cookie. Run the app and server on the same hostname. If the connection fails, check that the server is running and allows this app’s origin and your repository.</p></div></details>}
    </div>
  </div>;
}
