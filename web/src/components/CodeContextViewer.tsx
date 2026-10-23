import React, { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { FileCode2, Copy, Check } from 'lucide-react';
import type { DiffLine, ReviewChanges } from '@/lib/changes';
import type { ReviewFinding } from '@/lib/types';
import { InlineReviewComment } from './InlineReviewComment';

function CodeText({ text }: { text: string }) {
  // Rendering text nodes keeps repository content inert; no HTML injection or external grammar loader.
  return <code>{text.split(/("(?:[^"\\]|\\.)*"|'[^']*'|\/\/.*$|\b(?:public|private|class|return|async|await|new|var|int|string|void|using|namespace|Task|throw|if|null)\b)/g)
    .map((part, index) => <span key={index} className={part.startsWith('//') ? 'syntax-comment' : /^["']/.test(part) ? 'syntax-string' : /^(public|private|class|return|async|await|new|var|int|string|void|using|namespace|Task|throw|if|null)$/.test(part) ? 'syntax-keyword' : undefined}>{part}</span>)}</code>;
}

export function CodeContextViewer({ changes, findings, selectedFinding = null, onReturn, returnControlRef, active = true,
  focusRequest = selectedFinding, inspector, inspectorOpen = false, inspectorId, onToggleInspector }: {
  changes: ReviewChanges; findings: ReviewFinding[]; selectedFinding?: number | null; onReturn?: () => void;
  returnControlRef?: React.Ref<HTMLButtonElement>;
  active?: boolean; focusRequest?: number | null; inspector?: React.ReactNode; inspectorOpen?: boolean;
  inspectorId?: string; onToggleInspector?: () => void;
}) {
  const preferredFile = changes.files.find(file => file.analyzed && (file.source !== null || file.hunks.length > 0))
    ?? changes.files.find(file => file.source !== null || file.hunks.length > 0) ?? changes.files[0];
  const [path, setPath] = useState(preferredFile?.path ?? '');
  const [mode, setMode] = useState<'unified' | 'source'>('unified');
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState('');
  const contentRef = useRef<HTMLDivElement>(null);
  const filesRef = useRef<HTMLElement>(null);
  const codeRef = useRef<HTMLDivElement>(null);
  const paneScroll = useRef({ code: 0, files: 0, horizontal: 0 });
  const focusedRequest = useRef<number | null>(null);
  const navigatedRequest = useRef<number | null>(null);
  const pickerId = useId();
  const file = changes.files.find(item => item.path === path) ?? changes.files[0];
  const anchors = changes.anchors.filter(anchor => anchor.path === file?.path && findings[anchor.findingIndex]);
  useEffect(() => {
    if (!active || focusRequest === navigatedRequest.current) return;
    const anchor = changes.anchors.find(item => item.findingIndex === selectedFinding);
    if (!anchor) return;
    navigatedRequest.current = focusRequest;
    setPath(anchor.path);
    const target = changes.files.find(item => item.path === anchor.path);
    if (!target?.hunks.some(hunk => hunk.lines.some(line => line.newLine === anchor.startLine))) setMode('source');
  }, [selectedFinding, changes, active, focusRequest]);
  useLayoutEffect(() => {
    if (!active) return;
    if (contentRef.current) contentRef.current.scrollTop = paneScroll.current.code;
    if (filesRef.current) filesRef.current.scrollTop = paneScroll.current.files;
    if (codeRef.current) codeRef.current.scrollLeft = paneScroll.current.horizontal;
  }, [active]);
  useEffect(() => {
    if (!active || selectedFinding === null || focusRequest === focusedRequest.current) return;
    const target = contentRef.current?.querySelector<HTMLElement>(`#inline-finding-${selectedFinding}`);
    if (!target) return;
    focusedRequest.current = focusRequest;
    target?.scrollIntoView?.({ block: 'nearest', behavior: 'auto' });
    target?.focus({ preventScroll: true });
  }, [selectedFinding, path, mode, active, focusRequest]);

  if (!file) return <p className="workspace-empty">No changed files are available in this artifact.</p>;
  const lines: (DiffLine | { header: string })[] = mode === 'source'
    ? (file.source?.replace(/\n$/, '').split('\n') ?? []).map((text, index) => ({ kind: 'context', oldLine: null, newLine: index + 1, text }))
    : file.hunks.flatMap(hunk => [{ header: hunk.header }, ...hunk.lines]);
  const hiddenAnchors = anchors.filter(anchor => !lines.some(line => 'newLine' in line && line.newLine === anchor.startLine));
  return <div className={`code-workspace${inspectorOpen ? ' inspector-open' : ''}`} data-testid="code-context-viewer">
    <aside className="changed-files" ref={filesRef} aria-label="Changed files" onScroll={event => { paneScroll.current.files = event.currentTarget.scrollTop; }}>
      <div className="file-list-heading">Changed files <span>{changes.files.length}</span></div>
      <label className="mobile-file-label" htmlFor={pickerId}>Select changed file</label>
      <select id={pickerId} className="mobile-file-select" value={file.path} onChange={event => setPath(event.target.value)}>
        {changes.files.map(item => <option key={item.path} value={item.path}>{item.path} · {item.status}</option>)}
      </select>
      <div className="desktop-file-list">{changes.files.map(item => <button key={item.path} type="button" aria-current={file.path === item.path ? 'true' : undefined}
        onClick={() => { setPath(item.path); setCopied(false); }}><FileCode2 size={15} /><span>{item.path}</span><small>{item.status.slice(0, 1)}</small></button>)}</div>
      <p>Members in changed C# files are analyzed. Removed code and other files are outside that coverage.</p>
    </aside>
    <div className="code-main" ref={contentRef} onScroll={event => { paneScroll.current.code = event.currentTarget.scrollTop; }}>
      <div className="code-toolbar"><div><FileCode2 size={16} /><strong>{file.path}</strong><span className="quiet-badge">{file.status}</span></div>
        <div className="code-view-controls" aria-label="Source view">
          {onReturn && <button type="button" ref={returnControlRef} onClick={onReturn}>Back to finding</button>}
          {onToggleInspector && <button type="button" className="inspector-toggle" aria-controls={inspectorId}
            aria-expanded={inspectorOpen} onClick={onToggleInspector}>{inspectorOpen ? 'Hide findings' : 'Show findings'}</button>}
          <button type="button" aria-pressed={mode === 'unified'} onClick={() => setMode('unified')}>Unified diff</button>
          <button type="button" aria-pressed={mode === 'source'} onClick={() => setMode('source')}>Head source</button>
          {file.source && <button type="button" aria-label="Copy head source" onClick={async () => {
            try { await navigator.clipboard.writeText(file.source!); setCopied(true); setCopyError(''); }
            catch { setCopyError('Could not copy. Select and copy the source directly.'); }
          }}>{copied ? <Check size={14} /> : <Copy size={14} />}</button>}
        </div>
      </div>
      <div className="code-context-meta"><span>{file.analyzed ? 'C# analysis context' : 'Not analyzed in the head compilation'}</span>
        <span>head {changes.snapshot.headSha.slice(0, 8)}{mode === 'unified' && changes.snapshot.mergeBaseSha ? ` · merge-base ${changes.snapshot.mergeBaseSha.slice(0, 8)}` : ''}</span></div>
      {changes.unavailableReason && <p className="context-notice">{changes.unavailableReason}</p>}
      {file.oldPath && file.oldPath !== file.path && <p className="context-notice">Renamed from {file.oldPath}</p>}
      {file.unavailableReason && <p className="context-notice">{file.unavailableReason}</p>}
      {copyError && <p role="status" className="context-notice">{copyError}</p>}
      {hiddenAnchors.length > 0 && <p className="context-notice">{hiddenAnchors.length} comment(s) have member context outside these hunks. <button onClick={() => setMode('source')}>View in head source</button></p>}
      {lines.length ? <div className="code-scroll" ref={codeRef} onScroll={event => { paneScroll.current.horizontal = event.currentTarget.scrollLeft; }}
        tabIndex={0} role="region" aria-label={`${file.path} ${mode === 'source' ? 'head source' : 'unified diff'}`}>
        <table className="diff-table"><caption className="sr-only">{file.path} · {mode === 'source' ? 'Head source' : 'Unified diff'}. Minus means removed, plus means added.</caption><tbody>
          {lines.map((line, index) => 'header' in line ? <tr key={index} className="diff-hunk"><td colSpan={4}>{line.header}</td></tr> : <React.Fragment key={index}>
            <tr className={`diff-line diff-${line.kind}`}><td className="line-number">{line.oldLine}</td><td className="line-number">{line.newLine}</td>
              <td className="line-sign">{line.kind === 'added' ? '+' : line.kind === 'removed' ? '−' : ' '}</td><td className="source-text"><CodeText text={line.text} /></td></tr>
            {line.newLine !== null && anchors.filter(anchor => anchor.startLine === line.newLine).map(anchor => <tr key={`comment-${anchor.findingIndex}`} className="inline-comment-row"><td colSpan={4}>
              <InlineReviewComment anchor={anchor} finding={findings[anchor.findingIndex]} selected={selectedFinding === anchor.findingIndex} />
            </td></tr>)}
          </React.Fragment>)}
        </tbody></table>
      </div> : <p className="workspace-empty">{mode === 'source' ? 'No head source is available for this file.' : changes.snapshot.comparisonKind === 'head-only' ? 'Diff unavailable. Open Head source for the reviewed snapshot.' : 'No displayable line changes. Open Head source for context.'}</p>}
    </div>
    {inspector}
  </div>;
}
