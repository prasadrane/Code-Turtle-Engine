import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Code-Turtle Live — Interactive Council PR Review',
  description: 'AI code review council powered by Roslyn zero-hallucination compiler verification.',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className="dark">
      <body className="antialiased bg-slate-950 text-slate-100 min-h-screen">
        {children}
      </body>
    </html>
  );
}
