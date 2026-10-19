import React from 'react';
import Link from 'next/link';
import { ArrowUpRight } from 'lucide-react';
import { BrandLogo } from './BrandLogo';

export function SiteHeader() {
  return <header className="site-header">
    <div className="page-width nav-inner">
      <Link href="/" aria-label="Code Turtle home">
        <BrandLogo />
      </Link>
      <nav aria-label="Main navigation" className="main-nav">
        <Link href="/#workspace">Product</Link>
        <Link href="/#how-it-works">How it works</Link>
      </nav>
      <Link className="button button-small button-dark" href="/#workspace">Open workspace <ArrowUpRight size={15} />
      </Link>
    </div>
  </header>;
}
