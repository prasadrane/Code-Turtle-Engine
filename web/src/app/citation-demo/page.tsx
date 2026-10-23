import type { Metadata } from 'next';
import { CitationDemo } from '@/components/CitationDemo';

export const metadata: Metadata = { title: 'Citation verification demonstration' };
export default function CitationDemoPage() { return <CitationDemo />; }
