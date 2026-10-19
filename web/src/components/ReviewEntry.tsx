import React from 'react';
import { Play, KeyRound, Server, ArrowUpRight } from 'lucide-react';
import { PrInput } from './PrInput';

export function ReviewEntry({ onSample, onSubmit, accessToken, setAccessToken, initialUrl }: {
  onSample: () => void; onSubmit: (url: string) => void; accessToken: string;
  setAccessToken: (token: string) => void; initialUrl: string;
}) {
  return <div className="review-entry-grid">
    <aside className="sample-entry"><span className="entry-icon"><Play size={20} /></span><span className="eyebrow">EXPLORE AT YOUR OWN PACE</span>
      <h3>See a review in action.</h3><p>Explore an illustrative review with readable findings, source changes, and inline comments.</p>
      <button className="button button-green" onClick={onSample}>Explore sample review <ArrowUpRight size={16} /></button>
      <small>Authored sample findings. No sign-up, server, API key, or model calls.</small></aside>
    <div className="workspace-card live-entry"><div className="workspace-card-heading"><span className="entry-icon"><Server size={21} /></span><div><h3>Review with your local server.</h3><p>C# / .NET · Self-hosted beta</p></div></div>
      <p className="connection-prerequisite" id="connection-help">Live reviews require a configured trusted-local backend, an access key or active session, and a repository on your server’s allowlist. The public website runs the sample without these.</p>
      <PrInput onSubmit={onSubmit} onTryDemo={onSample} showDemo={false} initialUrl={initialUrl} />
      <details className="access-settings"><summary><KeyRound size={14} /> Connect your local beta server <span>Access key and connection help</span></summary>
        <div className="access-settings-body"><label htmlFor="access-key">Local beta access key</label>
          <input id="access-key" type="password" autoComplete="off" aria-describedby="access-help" value={accessToken} onChange={event => setAccessToken(event.target.value)} placeholder="Enter your server access key" />
          <p id="access-help">Use your self-hosted server’s key. It stays in page memory and is exchanged for a short-lived session cookie. Configure the backend origin with NEXT_PUBLIC_API_BASE when building this frontend. The default is http://localhost:5000; use the same hostname for frontend and backend.</p></div></details>
    </div>
  </div>;
}
