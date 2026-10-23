# Review App Separation — Design

Date: 2026-10-23
Status: Approved direction; reviewed by Codex (round 1 amendments folded in); ready for implementation (single pass with internal checkpoints)
Builder: Codex
Area: `web/` (Next.js app router, Vitest). All paths below are relative to `web/`.

## Intent

The review workspace (the product) currently lives as a section inside the marketing landing page. `src/app/page.tsx` renders `SiteHeader → HeroHeader → WorkflowStrip → ProductFeatures → <section id="workspace"> (ReviewEntry / ReviewWorkspace) → SiteFooter`. Starting the sample scrolls the page to that section (live start does not scroll, but renders in the same section). The workspace inherits the marketing palette, typography, and copy. The product reads as one more website section.

Goal: a visitor should feel a clear shift from *reading about Code Turtle* to *using Code Turtle*. Separation comes from owning the screen, navigation, and interaction — not from inverting the theme. One brand (turtle mark, forest green `#285b40`, shared sans-serif) across both.

## Goals

- Dedicated `/review` route with its own app shell. No `SiteHeader` / `SiteFooter` there.
- Landing page becomes marketing-only, keeping the illustrative `ReviewPreview` teaser.
- Light "workbench" app theme distinct from the cream editorial site.
- Launcher entry screen replacing the current embedded `ReviewEntry` presentation.
- Adaptive workspace: three panes in the desktop Changes view; single pane on mobile.
- Compact status bar for phase, reviewer progress, and sample playback controls.
- Starting the sample is a navigation event (`/review?sample=1`), not a scroll.

## Non-goals (deferred)

- Dark theme.
- Recent-reviews history.
- Shareable live-review URLs (needs a persistence and access contract; backend results are owner/session-bound and expire).
- App subdomain.
- Blocking browser Back/Forward during a live review (see Leave protection).

## Anti-goals

- Changing `useDemoReplay` or `useReviewStream` behavior or signatures.
- Weakening honesty copy (sample vs live provenance; citation checks establish symbol-name existence, not correctness).
- Regressing accessibility: skip link, focus return on reset, reduced-motion handling, aria labels, keyboard navigation in the workspace.
- Putting access keys, source, or findings in the URL.
- A viewport lock that clips controls at high zoom or short heights.

## Decided design

### Routes and ownership

| File | Responsibility |
|---|---|
| `src/app/page.tsx` | Marketing only: header, hero with `ReviewPreview`, workflow, features, footer. Remove review hooks/state. Make it a server component if nothing client-only remains. |
| `src/app/review/page.tsx` (new) | Server component. Exports `metadata` with `title: 'Review'` (root layout template already yields `Review \| Code Turtle`) and `robots: { index: false }`. Renders `<ReviewApp />` (inside `Suspense` if it reads `useSearchParams`). |
| `src/app/review/layout.tsx` (new) | App shell wrapper (`.app-shell`), skip-link target, imports `workspace.css`. |
| `src/components/ReviewApp.tsx` (new, `'use client'`) | Controller moved out of `HomePage`: mode, PR URL, access key, speed, both hooks, start/reset/retry, leave protection, `LanguageRejectionModal`. |
| New `AppBar` / `StatusBar` components | Rendered from controller props. |
| `ReviewEntry.tsx` | Reshaped into the launcher (below). Keep credential handling, disabled-submit logic, accessible setup reasons. |
| `ReviewWorkspace.tsx`, `CodeContextViewer.tsx`, `FinalReportView.tsx` | Adaptive composition. Hoist view state (see State preservation) so switching views or layouts does not reset it. |
| `HeroHeader.tsx`, `SiteHeader.tsx`, `ProductStory.tsx` | Replace `#workspace` anchors with `/review`; every sample CTA links to `/review?sample=1`. |
| `CitationDemo.tsx` (line ~54) | "Return to the review workspace" links to `/review` (currently `/`). |

No route groups needed; the root layout is only the document wrapper.

### App shell

```text
Logo → /    repository · PR #    Live|Sample    Status pill    [Cancel review] [New review]
────────────────────────────────────────────────────────────────────────────────
Provenance line (sample disclosure or live review identity)
Review overview | Changes | Evidence
────────────────────────────────────────────────────────────────────────────────
(content — see layout)
────────────────────────────────────────────────────────────────────────────────
Current phase        Reviewer progress (expandable)        Sample playback (pause/skip/1x 2x 5x)
```

- Task actions (Cancel, New review) live in the app bar. Playback controls live in the status bar next to replay progress.
- Personas/reviewers live in the status bar with expandable detail, not in the left rail.

### Theme (light workbench)

- Shell background ~`#eef1f3`; content panes white with clear neutral borders.
- Text charcoal ~`#25313a`.
- `#285b40` for primary actions and selection only. Errors, warnings, evidence outcomes, and diff add/remove keep their own distinguishable colors.
- Same sans-serif; fewer large headings, less editorial spacing. Monospace for paths, revisions, code, line numbers.
- Denser via reduced padding, not smaller prose/code sizes.
- Contrast: WCAG AA for text and interactive states.

### Workspace layout

Overview is the default view. Completion must not auto-switch the user's view.

**≥ ~1200px, Changes view:** file rail ~220px | code pane (flex, sensible min width) | findings inspector ~320–360px, collapsible. Panes scroll independently inside a viewport-height shell. The full `FinalReportView` (filters, persona breakdown, Markdown) stays in Overview, not in the inspector.

**Inspector contents:** all retained findings (not only the selected file's), reusing `FindingCard` and citation components, each showing whether it has a code anchor. Selecting an anchored finding switches to its file and focuses the anchor; unanchored findings stay readable in place.

**~768–1200px:** file rail collapses to a picker; inspector optional and collapses before the code pane gets cramped.

**< 768px:** one content pane at a time.
- Keep accessible Overview / Changes / Evidence tabs.
- Existing labeled changed-file selector above the diff.
- "View code context" opens Changes and focuses the anchored comment; "Back to finding" returns to the origin (see State preservation).
- Horizontal scroll stays inside code blocks.
- Status bar collapses to one compact summary; reviewer and playback details expand on demand. It must not cover focused inputs or code when the software keyboard is open: use in-flow layout (reflow), not a fixed overlay, and do not rely on input focus as a keyboard detector.
- One vertical scroller. At high zoom / short heights, reflow instead of locking.

Launcher may be vertically centered when space allows but must scroll naturally with the mobile keyboard open.

Existing constraints to preserve:
- `useReviewStream` fetches source artifacts (`/changes`) only once the review completes (both `completed` and `verdict` events trigger it). Changes view must explain pending/unavailable source while findings stream.
- Final-array anchors stay withheld from provisional findings.

### State preservation

`ReviewWorkspace` currently unmounts `FinalReportView` when opening Changes; `FinalReportView` keeps search, severity, persona filters, and active tab in local state; `CodeContextViewer` keeps source/diff mode locally. Hoist (or otherwise preserve across unmount) for the lifetime of one review:

- Overview: active report tab, search query, severity and persona filters, originating action.
- Changes: selected file, source/diff mode, selected finding.
- Scroll position of each pane that can be left and returned to.

Return behavior:
- Finding opened from Overview ("View code context") → "Back to finding" returns to Overview with filters intact and focuses the originating control.
- Finding selected in the Changes inspector → stays in Changes; focus returns to that inspector item.
- Replace the current document-wide, index-keyed return-focus selector with focus on the actual originating element, scoped to its view (overview and inspector may both render the same finding).

New review / reset clears all preserved state.

### Launcher

- Centered PR URL input + Start Review; "Try sample review" action (navigates to `/review?sample=1`).
- Compact connection chip: "Not configured", "Configuration issue", or "Configured · not checked". Auth/network failures shown only after an observed request.
- Long setup guidance collapsed (disclosure), with an accessible reason kept beside any disabled submit. Keep the link to the local setup guide.

### URL behavior and sample startup

- `/review` → launcher.
- `/review?sample=1` → one functioning replay, no duplicate events.
  - Initialization must survive React Strict Mode setup → cleanup → setup. Note `useDemoReplay`'s mount effect clears its timer on cleanup, so a naive "started" ref guard can leave the UI "playing" without advancing. The builder may use the hook's existing `autoStart` option or an equivalent pattern, as long as the result is one replay that advances.
  - "Once" means: ordinary rerenders never restart it; navigating into `?sample=1` again (link click, Back/Forward into that entry) starts a fresh replay.
  - All sample entry points (landing CTAs, launcher button, retry) use the same URL policy.
- Reset / New review clears sample intent (`router.replace('/review')`) and moves focus: PR field if enabled; otherwise (no server configured, field disabled) the launcher heading or the sample action.
- Live PR URL is never auto-submitted from the URL (if a param is added later, it only prefills).

### Leave protection (live reviews)

`useReviewStream` cleanup attempts `DELETE /api/review/{jobId}` on unmount when it has a job id and ignores failures; this is best effort and not guaranteed on reload or tab close. Treat leaving as stopping the review.

- Guard condition: `mode === 'live' && live.isStreaming` (covers submitting, queued, and running).
- Guarded app-owned actions: logo / any in-app link away from `/review`, New review / reset (stops the job without leaving). Show a confirmation that the review will be stopped; keep the controller mounted until the user confirms.
- `beforeunload`: best-effort warning only, while the guard condition holds.
- Browser Back/Forward: not blocked (App Router has no general navigation blocker; no history manipulation). Document this limitation in `web/README.md`.
- Sample replay needs no confirmation.

### CSS

Three stylesheets, one boundary each. Scoping under `.app-shell` stops app rules leaking out; app-only rules must also be moved out of `globals.css` so global rules do not style app descendants.

- `globals.css` (root import): reset, shared fonts/brand primitives, focus ring, reduced-motion, marketing styles. Move out the existing workspace / input / access-settings rules and their mobile overrides (from ~line 106 onward) to the files below.
- `review-content.css` (new, root import, after `globals.css`): styles for review components rendered outside the app (evidence, findings, badges, `TrustAuditVisualizer` on `/citation-demo`), including supporting selectors: report headings, explanatory text, chips, and their responsive rules. Give these components intentional defaults that work on both cream and workbench surfaces.
- `workspace.css` (imported by `src/app/review/layout.tsx` only): all app-only rules scoped under `.app-shell`, including the wrapper's tokens, base text color, and background. Remove the global `workspace.css` import from `src/app/layout.tsx`.
- Resolve the `.line-number` collision (teaser in `globals.css` ~lines 67, 71, 213, 214 vs diff in `workspace.css` ~lines 155, 200): rename the teaser's class (e.g. `.preview-line-number`) in its JSX and all four teaser selectors together.

## Tests

Baseline: 14 Vitest files, 119 tests passing.

- `tests/landing-page.test.tsx`: keep marketing/preview assertions; assert CTA hrefs (`/review`, `/review?sample=1`); move review lifecycle scenarios to a new `tests/review-app.test.tsx`.
- `tests/review-connection-entry.test.tsx`: render via the new controller for integration cases; keep connection and credential protections.
- `tests/review-workspace.test.tsx`: keep deterministic anchoring, provisional restrictions, unavailable-source behavior, keyboard navigation, focus return.
- `tests/citation-demo.test.tsx`: add an assertion that the return link points to `/review`.
- New coverage in `tests/review-app.test.tsx`:
  - Under `React.StrictMode`, `?sample=1` with the real `useDemoReplay` advances through to completion with no duplicate findings (do not just count `start()` calls).
  - Rerender does not restart the sample; re-entering `?sample=1` starts fresh.
  - Reset clears the param; focus goes to the PR field when configured and to the fallback target when unconfigured.
  - Leave confirmation appears for guarded actions only while `mode === 'live' && isStreaming`; cancelling keeps the review running; sample shows no confirmation.
  - Overview filters and selected file/mode survive Overview → Changes → Back; focus returns to the originating control (overview and inspector cases).

Done criteria: `npm test`, typecheck, and `npm run build` pass. Manual browser check at 375px, ~1000px, ≥1280px, and 200% zoom, including a real software keyboard on mobile: pane scrolling, status bar not overlapping, CSS isolation on `/`, `/review`, `/citation-demo`, and navigation between routes in both directions.

Docs: add a Routes section to `web/README.md` describing `/` vs `/review` vs `/citation-demo` and the live-review leave behavior.

## Implementation order (one delivery, internal checkpoints)

1. Add regression tests for sample startup, reset focus, and leave behavior (failing where behavior is new).
2. Extract shared CSS and rename teaser line numbers; verify `/` and `/citation-demo` unchanged.
3. Move the controller to `/review`, keeping the existing workspace composition. Verify sample URLs, reset, Back/Forward, live cleanup.
4. Preserve view state; then add panes and the inspector.
5. Finish launcher, theme, and status bar.
6. Full suite, typecheck, production build, browser checks.

## Left to the builder

- Exact component names for app bar / status bar and file split within `ReviewWorkspace`.
- Mechanism for preserved view state (lifted state, context, or keyed store) as long as the listed behavior holds.
- Exact token values within the stated ranges, subject to AA contrast.
- Breakpoint fine-tuning around 768 / 1200.
- Transition styling for entering `/review` (respect `prefers-reduced-motion`).

## Calls made by Claude (not explicitly confirmed by the owner)

- Leave confirmation for app-owned navigation and New review during live reviews; `beforeunload` as best effort; browser Back not blocked.
- Three stylesheets with `.app-shell` scoping, app-only rules moved out of `globals.css`; teaser class renamed to resolve `.line-number`.
- `noindex` and `title: 'Review'` for `/review`.
- Mobile status bar reflows rather than overlapping the software keyboard.
- Adopted Codex defaults: inspector lists all retained findings with anchor availability; re-entering `?sample=1` starts a fresh replay; inspector-originated findings return focus within Changes.
