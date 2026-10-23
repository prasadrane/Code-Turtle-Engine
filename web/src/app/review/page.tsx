import React, { Suspense } from 'react';
import type { Metadata } from 'next';
import { ReviewApp } from '@/components/ReviewApp';

export const metadata: Metadata = { title: 'Review', robots: { index: false } };

export default function ReviewPage() {
  return <Suspense fallback={<main id="review-main" aria-busy="true">Opening review workspace…</main>}>
    <ReviewApp />
  </Suspense>;
}
