# Code Turtle review workspace redesign

Date: 2026-10-19. Status: proposed implementation plan; product code is unchanged.

## Outcome

Make the actual review workspace share the light, calm, code-centered appearance
of the illustrative hero card supplied by the user. Keep the Code Turtle identity
and existing review behavior. Keep the developer style guide outside the app and
product READMEs.

Deliver this in two passes: the working UI using existing review data, followed by
real source context and diff support. Changing the UI does not require changing
the model's compiler-fact input contract.

## What the code currently supports

Source inspected at commit `74693ff`; source reads were used to verify graph results
because graph coverage reported changed metadata.

| Current behavior | Source |
|---|---|
| The hero's code snippet and finding marker are illustrative JSX | [ReviewPreview.tsx](../../../web/src/components/ReviewPreview.tsx) |
| The actual workspace stacks timeline, persona panels, audit, and final report | [page.tsx](../../../web/src/app/page.tsx) |
| Network errors stop streaming, while the page badge only distinguishes complete/in progress | [useReviewStream.ts](../../../web/src/hooks/useReviewStream.ts), [page.tsx](../../../web/src/app/page.tsx) |
| The displayed citation percentage is zero when there are no audit records | [TrustAuditVisualizer.tsx](../../../web/src/components/TrustAuditVisualizer.tsx) |
| GitHub acquisition retains file names and commit identifiers, not patch hunks | [GitHubPrFetcher.cs](../../../src/CodeTurtleEngine.Web/Services/GitHubPrFetcher.cs) |
| Diff providers return changed C# paths, not displayable line diffs | [DiffProvider.cs](../../../src/CodeTurtleEngine.Gatekeeper/DiffProvider.cs), [GitHubPrDiffProvider.cs](../../../src/CodeTurtleEngine.Web/Services/GitHubPrDiffProvider.cs) |
| Payload generation analyzes members in each changed file; it does not currently restrict them to changed-line intersections | [PayloadGenerator.cs](../../../src/CodeTurtleEngine.Gatekeeper/PayloadGenerator.cs) |
| Product prompts serialize Roslyn facts; raw source has a separate benchmark-only path | [Payload.cs](../../../src/CodeTurtleEngine.Core/Payload.cs), [PromptBuilder.cs](../../../src/CodeTurtleEngine.Council/PromptBuilder.cs) |
| Browser events/results expose findings and audits, without source/diff DTOs | [SseEvents.cs](../../../src/CodeTurtleEngine.Web/Models/SseEvents.cs), [Program.cs](../../../src/CodeTurtleEngine.Web/Program.cs), [types.ts](../../../web/src/lib/types.ts) |
| Acquisition fetches only the exact head commit with depth 1 | [RepoCloner.cs](../../../src/CodeTurtleEngine.Web/Services/RepoCloner.cs) |
| The cloned checkout is cleaned up after the review | [WebReviewPipeline.cs](../../../src/CodeTurtleEngine.Web/Services/WebReviewPipeline.cs) |

These are implementation observations, not a new live review or hosted-service test.

## Pass 1: Working UI and correct states

### Visual system

- Paper `#F8F9F5` around the workspace; white cards and code surfaces.
- Ink `#203B30` headings/text; forest `#285B40` primary controls.
- Sage `#E9EEE4` separators and supporting surfaces; lime `#D6EDB6` subtle accents.
- System sans serif for product copy, monospace for code, paths, and identifiers.
- Flat, aligned panels with restrained borders, shadows, and 7–10 px corner radii.
- Functional body copy around 14 px, readable code around 13 px, and accessible
  labels. Scale the illustrative card's composition to real reading sizes.
- Place review status and evidence summaries inside the workspace header/footer.
- Update the internal style guide's existing dark-console rule to reflect the
  newly requested light workspace when this pass is implemented.

### Workspace composition

```text
┌ Repository / PR # · title · analyzed revision · status ───────────────┐
│ Review overview        Changes        Evidence            Actions   │
├────────────────────────────────────────────────────────────────────┤
│ Compact progress row, or an actionable failure message              │
├──────────────────────┬─────────────────────────────────────────────┤
│ Changed C# files     │ Selected file / member context               │
│ and finding counts   │ Source or unified diff · old/new line nums   │
│                      │ Findings anchored where evidence permits    │
├──────────────────────┴─────────────────────────────────────────────┤
│ Prioritized review notes · severity/persona/search filters          │
│ Expand a note for rationale, citations, and available code context  │
├────────────────────────────────────────────────────────────────────┤
│ Performance · Security · Architecture     Citation evidence summary │
└────────────────────────────────────────────────────────────────────┘
```

- **Review overview:** prioritized retained findings, selected finding context,
  review coverage, and compact status for the three reviewers.
- **Changes:** full file navigation and source/diff inspection, when real data exists.
- **Evidence:** detailed citation audits, rejected/unavailable evidence, and raw
  persona audit output. Keep synthesized and raw findings distinguishable.
- Keep Markdown export/copy and review controls in an actions area.
- Keep Speedy, Sheldon, and Sensei as small recognizable reviewer identities;
  move large avatar panels and quips into expandable reviewer details.
- Use the turtle mark, subtle syntax colors, and restrained green highlights to
  connect the workspace visually with the illustration.

Pass 1 must work with existing SSE and replay data. When source context is absent,
show a clear unavailable state and use findings as the primary content. Never
substitute the illustrative `GetOrderAsync` snippet into a real review.

### State model

Represent request/review state explicitly:

`idle → submitting → queued/running → completed | failed | cancelled`

- A failed start request becomes **Could not start review**, with Retry, Try sample,
  and server connection help. Do not leave it labeled In progress.
- Preserve the input URL and useful recovery context after a failure.
- Inspect request status/health/configuration during implementation before deciding
  whether a failed fetch came from an unavailable server, CORS, or API access policy.
- Treat empty/pending/unavailable citation evidence as **Not evaluated** or **—**.
  Show a percentage only when actual audit records support its denominator.
- Preserve provisional findings, degraded participation, unavailable legacy demo
  provenance, and the distinction between citation existence and correctness.
- On mobile, use a file picker, stacked code/notes, and contained horizontal code
  scrolling. Keep controls usable with keyboard input and reduced motion.

### Implementation boundaries

Introduce focused components such as `ReviewWorkspace`, `ReviewHeader`,
`ReviewStatus`, `ReviewNotes`, `ReviewerStrip`, and `EvidencePanel`.

Refactor `page.tsx`, the existing council/report/audit components, review-state
handling, and shared styles. Prefer semantic surface/text/status tokens over
global substitutions of dark Tailwind color utilities. Keep all existing filters,
replay controls, raw audits, and exports reachable.

## Pass 2: Real source context and diffs

### The source/model distinction

Roslyn builds syntax/semantic models from the checked-out C# source. This is not
a product that receives only compiled binaries. The model is given structured
facts derived from those compilations, rather than raw source bodies.

Keep two separate data paths:

```text
Pinned source snapshot → Roslyn → structured facts → model → findings
Pinned source snapshots → source/diff artifact → browser code viewer
```

Displaying raw source in the browser is compatible with keeping it out of model
prompts. It does introduce a separate source-delivery path that must be explicit;
do not add source text or diff hunks to `RoslynPayload` or its prompt projection.

### Snapshot and comparison rules

1. Capture immutable PR metadata: repository, number/title, base SHA, analyzed head
   SHA, and comparison semantics. Revalidate the snapshot if the PR changes during
   acquisition so file lists, code, diffs, and findings describe the same revision.
2. Offer the analyzed **head source snapshot** for context. This alone is not a diff.
3. For a GitHub-style PR diff, compare merge-base to the analyzed head. Do not
   silently label a base-tip-to-head comparison as the same thing.
4. The existing shallow head checkout needs a bounded acquisition extension for
   the base/history necessary to establish that merge base. Use exact identities;
   show Diff unavailable if the required objects cannot be obtained reliably.
5. Create the display artifact before clone cleanup. Retain it with the review
   job and expire it with the job, rather than reading a deleted checkout later.

### Display contract

Add a web-specific display DTO, separate from the model payload:

- Review identity, base/head/merge-base identifiers, and comparison kind.
- File old/new paths, add/modify/delete/rename status, and supported scope.
- Hunks with old/new line starts; added, removed, and context line records.
- Explicit availability/omission reasons for missing, oversized, or non-text data.
- Head-snapshot member ranges and project context where available from Roslyn.

Serve a bounded artifact through a proposed `GET /api/review/{jobId}/changes`
endpoint. Bind access to the authorized review/session, enforce path and size
limits, escape source as text, and keep source out of logs and persistent browser
storage. Preserve existing trusted-local acquisition restrictions.

Start with reviewed C# files. Clearly label other files and removed code that were
not analyzed in the head compilation; a visible diff does not imply review coverage.

### Finding anchors

- Git determines which lines changed. Roslyn determines known member ranges and
  semantic facts. The model's free-text `Location` is an unverified suggestion.
- Create a clickable anchor only when it resolves against the pinned file/revision
  and an appropriate deterministic range; show the anchor's provenance.
- Where project identity, overloads, member names, or locations are ambiguous,
  leave the finding unanchored and allow manual file inspection.
- A citation-existence check alone cannot prove the model identified the right
  line, overload, call, or defect. Do not turn it into a verified line marker.
- Changed-hunk-to-member intersection is a separate scope decision: the current
  extractor analyzes members throughout each changed file. Label broader context
  instead of implying every finding is on a changed line.

### Viewer and demo

Build a read-only `CodeContextViewer` / `DiffViewer` with syntax highlighting,
line numbers, file selection, unified diff first, and split view for wide screens.
Keep focus, selection, source copy, and finding navigation consistent.

Extend frontend types/hooks for display data. Add a versioned, clearly labeled
demo fixture with known base/head source and generated diff artifacts to exercise
the viewer. Preserve historical replay and benchmark provenance; do not invent
fresh compiler or model results for an illustrative demo.

## Validation and delivery order

1. Document the proposed light workspace, state transitions, and component layout.
2. Implement Pass 1 against existing replay and mocked live events.
3. Validate idle, running, failed start, failed stream, compilation failure,
   cancellation, degraded review, no retained findings, and unavailable evidence.
4. Implement the snapshot/display contract and backend artifact lifecycle.
5. Test known Git fixtures: additions, deletions, renames, shifted line numbers,
   missing bases, truncated/omitted content, and ambiguous finding anchors.
6. Verify raw source/diffs never enter grounded prompt construction; verify
   authorized artifact access and expiry independently of model-quality checks.
7. Integrate the viewer and validate selected finding/file/line behavior end to end.
8. Run relevant frontend and backend tests, TypeScript, and the production build.
   Inspect desktop and mobile layouts; refresh product and review screenshots.

Visual completion means the actual working workspace follows the reference's
light composition and readable hierarchy. Diff completion additionally requires
real pinned source/diff data and truthful anchors. Neither milestone establishes
live-model accuracy or public deployment readiness.
