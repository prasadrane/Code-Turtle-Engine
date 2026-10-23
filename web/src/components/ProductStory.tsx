import React from 'react';
import Link from 'next/link';
import { ArrowUpRight, ScanLine, UsersRound, ShieldCheck, GitPullRequest, FileCheck2, Code2 } from 'lucide-react';
import { BrandLogo, TurtleMark } from './BrandLogo';

const features = [
  { icon: ScanLine, number: '01', title: 'Context before commentary.', description: 'Roslyn extracts compiler facts from members in changed C# files, giving the council structured context to work with.', label: 'COMPILER-GROUNDED' },
  { icon: UsersRound, number: '02', title: 'Three minds on the details.', description: 'Dedicated AI personas examine performance, security, and architecture in parallel. Different perspectives, one place.', label: 'AI REVIEW COUNCIL' },
  { icon: ShieldCheck, number: '03', title: 'A trail you can follow.', description: 'Citation checks show which type and member names exist. You stay in control of assessing each recommendation.', label: 'ROSLYN CITATION GUARD' },
];

export function ProductFeatures() {
  return <section className="feature-section page-width" id="how-it-works" aria-labelledby="features-title">
    <div className="section-heading">
      <div>
        <span className="eyebrow">LESS NOISE. MORE PERSPECTIVE.</span>
        <h2 id="features-title">Good reviews start with a closer look.</h2>
      </div>
      <p>A thoughtful partner in your workflow.<br />Your judgment stays at the center.</p>
    </div>
    <div className="feature-grid">{features.map(({ icon: Icon, number, title, description, label }) => <article className="feature-card" key={number}>
      <div className="feature-top">
        <span className="feature-icon">
          <Icon size={21} strokeWidth={1.6} />
        </span>
        <span className="feature-number">{number}</span>
      </div>
      <h3>{title}</h3>
      <p>{description}</p>
      <span className="feature-label">{label}</span>
    </article>)}</div>
  </section>;
}

export function WorkflowStrip() {
  return <div className="workflow-strip page-width" aria-label="Review workflow">
    <span className="workflow-label">FROM PULL REQUEST TO PERSPECTIVE</span>
    <span>
      <GitPullRequest size={16} /> Add your PR</span>
    <i />
    <span>
      <Code2 size={17} /> Let the council look</span>
    <i />
    <span>
      <FileCheck2 size={17} /> Make the call</span>
  </div>;
}

export function SiteFooter() {
  return <footer className="site-footer">
    <div className="page-width footer-main">
      <div>
        <Link href="/" aria-label="Code Turtle home">
          <BrandLogo />
        </Link>
        <p>Review with care. Ship with confidence.</p>
      </div>
      <div className="footer-links">
        <Link href="/review">Workspace <ArrowUpRight size={13} />
        </Link>
      </div>
      <TurtleMark className="footer-turtle" />
    </div>
    <div className="page-width footer-bottom">
      <span>© {new Date().getFullYear()} Code Turtle</span>
      <span>Thoughtful by nature. Precise by design.</span>
      <span className="beta-label">
        <span className="status-dot" /> Self-hosted beta</span>
    </div>
  </footer>;
}
