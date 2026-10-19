# Code Turtle developer style guide

Repository-only guidance for maintaining the product identity and UI. This is
developer documentation, not an app page or a public download.

Tagline: **Review with care. Ship with confidence.**

## Positioning
A thoughtful AI review council for C# pull requests. Three review perspectives,
compiler facts, and citation existence checks help developers examine changes.
The product is currently a self-hosted beta. The browser sample is a recorded replay.

## Identity
The original geometric turtle icon carries code brackets in its shell. It represents
care, protection, and steady progress. Use `Code Turtle` in prose and the supplied
lowercase `codeturtle.` wordmark in the logo.

- Primary: code-turtle-logo.svg on Paper or white.
- Reversed: code-turtle-logo-reversed.svg on Ink.
- Icon: code-turtle-icon.svg, also used as the browser favicon.
- SVG wordmarks contain live text; use the specified font stack, or outline text
  in your design tool for typography identical across exported print files.
- Clear space: half the icon height on each side. Minimum icon size: 24 px.
- Do not stretch, rotate, recolor, or place the logo over busy imagery.

## Color
| Token | Value | Use |
| --- | --- | --- |
| Forest | #285B40 | Primary buttons and icon |
| Ink | #203B30 | Body text, headings, dark identity surface |
| Paper | #F8F9F5 | Main page background |
| Sage | #E9EEE4 | Quiet backgrounds |
| Lime | #D6EDB6 | Accent and reversed logo |
| Slate leaf | #68746B | Supporting text |
| Line | #DFE5DA | Borders and dividers |
| White | #FFFFFF | Cards and primary button labels |

Use Ink or Forest for functional text on pale surfaces. Use white on Forest buttons.
Small decorative text in illustrative artwork is not a template for functional UI.
Do not rely on color alone for severity, progress, or errors.

## Typography and layout
- UI: Segoe UI, Apple system fonts, Arial, sans-serif. No remote font dependency.
- Code: Cascadia Code, SFMono-Regular, Consolas, monospace.
- Headings: regular/medium, tight letter spacing, 1.1–1.25 line height.
- Body: regular, 1.7–1.85 line height; label buttons with a clear action.
- Content width: 1200 px. Base spacing: 4 px. Control radius: 7 px; cards: 10 px.
- Light marketing and input surfaces, dark forest review console, semantic severity accents.
- Support visible keyboard focus, 320 px layouts, and reduced motion.

## Voice
Helpful, grounded, human. Use plain words and specific claims. Keep developers in control.
Say "citation existence checked"; do not imply that a recommendation is proven correct.
Describe sample data as sample data. Avoid unsupported accuracy, speed, customer, or ROI claims.

## Assets
The SVG logo assets live in `web/public/brand/` and are used by the app and README
headers. [theme.css](theme.css) provides portable reference tokens for developers.
The application uses the same palette in `web/src/app/globals.css` and the forest
review palette in `web/tailwind.config.ts`.

This guide defines a proposed product identity. It does not establish name/trademark
availability, production hosting, billing, or commercial launch readiness.
