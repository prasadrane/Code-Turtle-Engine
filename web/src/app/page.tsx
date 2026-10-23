import React from 'react';
import { HeroHeader } from '@/components/HeroHeader';
import { SiteHeader } from '@/components/SiteHeader';
import { ProductFeatures, WorkflowStrip, SiteFooter } from '@/components/ProductStory';

export default function HomePage() {
  return <>
    <a href="#main-content" className="skip-link">Skip to main content</a>
    <SiteHeader />
    <main id="main-content"><HeroHeader /><WorkflowStrip /><ProductFeatures /></main>
    <SiteFooter />
  </>;
}
