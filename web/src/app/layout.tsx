import type { Metadata } from 'next';
import './globals.css';
import './workspace.css';

export const metadata: Metadata = {
  title: { default: 'Code Turtle — Review with care. Ship with confidence.', template: '%s | Code Turtle' },
  description: 'A thoughtful AI review council for C# pull requests. Explore performance, security, and architecture with compiler facts and citation existence checks.',
  applicationName: 'Code Turtle',
  icons: { icon: '/brand/code-turtle-icon.svg' },
  openGraph: { title: 'Code Turtle', description: 'Review with care. Ship with confidence.', type: 'website' },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return <html lang="en"><body>{children}</body></html>;
}
