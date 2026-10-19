import React from 'react';
import { GitPullRequest, ShieldCheck, ArrowUpRight, Check, Code2, Zap, LockKeyhole } from 'lucide-react';
import { TurtleMark } from './BrandLogo';

export function ReviewPreview() {
  return (
    <div className="preview-scene" aria-label="Illustrative sample code review">
      <div className="orbit orbit-one" />
      <div className="orbit orbit-two" />
      <span className="scene-note">A second set of eyes.<br />A little peace of mind.</span>
      <div className="preview-window">
        <div className="window-toolbar">
          <div className="window-dots">
            <i />
            <i />
            <i />
          </div>
          <span>code turtle / review</span>
          <span className="preview-sample">SAMPLE</span>
        </div>
        <div className="preview-pr">
          <span className="pr-icon">
            <GitPullRequest size={20} />
          </span>
          <div>
            <span className="preview-repo">sample-service <span>/ pull request #42</span>
            </span>
            <h2>A closer look at your changes</h2>
          </div>
          <ArrowUpRight size={17} />
        </div>
        <div className="preview-tabs">
          <span className="selected">
            <Code2 size={13} /> Review overview</span>
          <span>3 perspectives</span>
          <span className="preview-language">C#</span>
        </div>
        <div className="code-block" aria-label="Illustrative C# code snippet">
          <div>
            <span className="line-number">12</span>
            <code>
              <span className="syntax-purple">public async</span> Task&lt;Order&gt;</code>
          </div>
          <div>
            <span className="line-number">13</span>
            <code>  <span className="syntax-green">GetOrderAsync</span>(<span className="syntax-purple">int</span> id)</code>
          </div>
          <div>
            <span className="line-number">14</span>
            <code>{'{'}</code>
          </div>
          <div className="code-highlight">
            <span className="line-number">15</span>
            <code>  <span className="syntax-purple">return await</span> _orders.FindAsync(id);</code>
            <span className="code-pin">1</span>
          </div>
          <div>
            <span className="line-number">16</span>
            <code>{'}'}</code>
          </div>
        </div>
        <div className="preview-finding">
          <div className="finding-avatar">
            <TurtleMark />
          </div>
          <div>
            <div className="finding-title">Small details. Better decisions.<span>REVIEW NOTE</span>
            </div>
            <p>See what each reviewer found, then inspect the cited symbols before deciding what to change.</p>
            <span className="citation-chip">
              <ShieldCheck size={12} /> Citation existence checks</span>
          </div>
        </div>
        <div className="preview-reviewers">
          <span>
            <Zap size={13} /> Performance</span>
          <span>
            <LockKeyhole size={13} /> Security</span>
          <span>
            <Code2 size={13} /> Architecture</span>
        </div>
        <div className="preview-footer">
          <span>
            <span className="status-dot" /> Three perspectives. One review.</span>
          <span>Made for humans <Check size={12} />
          </span>
        </div>
      </div>
      <div className="floating-note">
        <span className="floating-icon">
          <ShieldCheck size={20} />
        </span>
        <div>
          <strong>Grounded in compiler facts.</strong>
          <span>Clarity, right down to the symbols.</span>
        </div>
      </div>
      <span className="preview-caption">Illustrative preview · explore a recorded review below</span>
    </div>
  );
}
