import type { Metadata } from 'next';
import { Download, ArrowDownToLine } from 'lucide-react';
import { BrandLogo } from '@/components/BrandLogo';
import { SiteHeader } from '@/components/SiteHeader';
import { SiteFooter } from '@/components/ProductStory';

export const metadata: Metadata = { title: 'Brand & assets', description: 'The Code Turtle identity: logo downloads, palette, typography, and writing principles.' };
const colors = [
  { name: 'Forest', hex: '#285B40' }, { name: 'Ink', hex: '#203B30' },
  { name: 'Paper', hex: '#F8F9F5' }, { name: 'Sage', hex: '#E9EEE4' },
  { name: 'Lime', hex: '#D6EDB6' }, { name: 'Slate leaf', hex: '#68746B' },
];

export default function BrandPage() {
  return <>
    <SiteHeader brandPage />
    <main className="brand-page page-width">
      <div className="brand-intro">
        <span className="eyebrow">THE CODE TURTLE BRAND</span>
        <h1>Thoughtful by nature.<br />Precise by design.</h1>
        <p>A calm identity for a careful code reviewer. The turtle represents care and steady progress; code brackets in its shell bring the product into focus.</p>
      </div>
      <section className="brand-lockups" aria-label="Logo downloads">
        <article className="brand-lockup">
          <BrandLogo />
          <div>
            <span>Primary wordmark</span>
            <a href="/brand/code-turtle-logo.svg" download>
              <Download size={14} /> Download SVG</a>
          </div>
        </article>
        <article className="brand-lockup brand-lockup-dark">
          <BrandLogo />
          <div>
            <span>Reversed wordmark</span>
            <a href="/brand/code-turtle-logo-reversed.svg" download>
              <Download size={14} /> Download SVG</a>
          </div>
        </article>
      </section>
      <section className="brand-section" aria-labelledby="palette-title">
        <span className="eyebrow">01 / COLOR</span>
        <h2 id="palette-title">A little closer to nature.</h2>
        <div className="brand-swatches">{colors.map(color => <div className="brand-swatch" key={color.name}>
          <div style={{ backgroundColor: color.hex }} />
          <p>{color.name}<code>{color.hex}</code>
          </p>
        </div>)}</div>
      </section>
      <div className="brand-guidance">
        <section className="brand-section">
          <span className="eyebrow">02 / TYPOGRAPHY</span>
          <h2>Clear at every scale.</h2>
          <div className="type-sample">Review with care.<br />Ship with confidence.</div>
          <p>Use the system sans serif stack for a familiar, crisp interface: Segoe UI on Windows, the system font on Apple devices, and Arial as fallback. Use regular or medium headings and generous spacing.</p>
          <div className="type-code">{'await review.WithCare();'}</div>
          <p>Cascadia Code, SFMono-Regular, or Consolas for code, identifiers, and compact labels. No external font request is required.</p>
        </section>
        <section className="brand-section">
          <span className="eyebrow">03 / VOICE & USAGE</span>
          <h2>Helpful. Grounded. Human.</h2>
          <p>Write with the care you bring to a code review. Be specific about what a finding means. Describe compiler facts and citation checks precisely. Invite the developer to make the final call.</p>
          <p>Use the name <strong>Code Turtle</strong> in prose. The lowercase wordmark is reserved for the logo. The tagline is <strong>“Review with care. Ship with confidence.”</strong>
          </p>
          <p>Keep clear space equal to half the mark’s height around the logo. Use the icon at 24 px or larger. Choose the lime wordmark on ink backgrounds. Keep proportions and colors intact.</p>
          <p>Forest green is for primary actions; ink is for text. Sage and lime are surfaces and accents. Use severity labels and icons alongside color.</p>
        </section>
      </div>
      <section className="brand-section">
        <span className="eyebrow">04 / TAKE IT WITH YOU</span>
        <h2>One identity. Every touchpoint.</h2>
        <div className="brand-downloads">
          <a className="button button-green" href="/brand/code-turtle-icon.svg" download>
            <ArrowDownToLine size={16} /> Turtle icon</a>
          <a className="button button-dark" href="/brand/theme.css" download>
            <ArrowDownToLine size={16} /> Theme tokens</a>
          <a className="button button-dark" href="/brand/brand-guide.md" download>
            <ArrowDownToLine size={16} /> Brand guide</a>
        </div>
      </section>
    </main>
    <SiteFooter />
  </>;
}
