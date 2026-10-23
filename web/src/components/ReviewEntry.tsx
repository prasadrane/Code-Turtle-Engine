import React from 'react';
import { Play, KeyRound, Server, ArrowUpRight } from 'lucide-react';
import { PrInput } from './PrInput';

export const LOCAL_SETUP_GUIDE = 'https://github.com/prasadrane/Code-Turtle-Engine/blob/main/docs/beta/BETA.md#run-the-local-web-ui';

export function ReviewEntry({ onSample, onSubmit, accessToken, setAccessToken, initialUrl, connectionError, connectionState }: {
  onSample: () => void; onSubmit: (url: string) => void; accessToken: string;
  setAccessToken: (token: string) => void; initialUrl: string; connectionError?: string | null;
  connectionState?: 'unconfigured' | 'issue' | 'configured';
}) {
  const state = connectionState ?? (connectionError ? 'unconfigured' : 'configured');
  return <div className="review-entry">
    <p className="launcher-intro">Bring a C# / .NET pull request to your local review council.</p>
    <PrInput onSubmit={onSubmit} onTryDemo={onSample} showDemo={false} initialUrl={initialUrl}
      disabled={Boolean(connectionError)} descriptionId="connection-help" presets={[]} />
    <div className="launcher-connection"><span className={`connection-chip connection-${state}`}><Server size={14} />
      {state === 'unconfigured' ? 'Not configured' : state === 'issue' ? 'Configuration issue' : 'Configured · not checked'}</span>
      <p className="connection-prerequisite" id="connection-help" role={connectionError ? 'status' : undefined}>{connectionError
        ? <><strong>Local server setup needed.</strong> {connectionError}</>
        : 'Uses your trusted local server and a repository on its allowlist.'}</p>
    </div>
    <details className="launcher-setup access-settings"><summary><KeyRound size={14} />Local server setup and access key</summary>
      <div className="access-settings-body">
        <p>Live reviews need a running trusted-local server, an access key or active session, and an allowed repository.</p>
        <p><a className="text-link" href={LOCAL_SETUP_GUIDE}>Set up local reviews <ArrowUpRight size={14} /></a></p>
        {!connectionError && <><label htmlFor="access-key">Local beta access key</label>
          <input id="access-key" type="password" autoComplete="off" aria-describedby="access-help" value={accessToken} onChange={event => setAccessToken(event.target.value)} placeholder="Enter your server access key" />
          <p id="access-help">Use your local server’s key. It stays in page memory and is exchanged for a short-lived session cookie.
            Run the app and server on the same hostname. If the connection fails, check that the server is running and allows this app’s origin and your repository.</p></>}
      </div>
    </details>
    <div className="launcher-sample"><button type="button" className="button" onClick={onSample}><Play size={15} />Try sample review<ArrowUpRight size={15} /></button>
      <p>Illustrative sample. Authored findings and fixture source/diff. No sign-up, server, API key, or model calls.</p>
    </div>
  </div>;
}
