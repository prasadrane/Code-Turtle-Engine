import React from 'react';
import Link from 'next/link';
import { ArrowRight, Play, Check, ArrowUpRight } from 'lucide-react';
import { ReviewPreview } from './ReviewPreview';

export interface HeroHeaderProps {
  className?: string;
}

export function HeroHeader({ className = '' }: HeroHeaderProps) {
  return (
    <section className={`hero page-width ${className}`} aria-labelledby="hero-title">
      <div className="hero-copy">
        <div className="eyebrow">
          <span className="status-dot" /> A THOUGHTFUL SECOND LOOK AT YOUR CODE</div>
        <h1 id="hero-title">Review with care.<br />
          <span>Ship with<br className="desktop-break" /> confidence.</span>
        </h1>
        <p className="hero-description">Meet Code Turtle. Your AI review council for C# pull requests, bringing performance, security, and architecture into focus.</p>
        <div className="hero-actions">
          <Link href="/review?sample=1" className="button button-green">
            <Play size={15} fill="currentColor" /> Try a sample review <ArrowRight size={17} />
          </Link>
          <a className="text-link" href="#how-it-works">See how it works <ArrowUpRight size={15} />
          </a>
        </div>
        <div className="hero-assurances">
          <span>
            <Check size={14} /> No sign-up for the demo</span>
          <span>
            <Check size={14} /> Built for C# / .NET</span>
        </div>
      </div>
      <ReviewPreview />
    </section>
  );
}
