<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="public/brand/code-turtle-logo-reversed.svg">
    <img src="public/brand/code-turtle-logo.svg" alt="Code Turtle" width="300">
  </picture>
</p>

# Code Turtle · Web UI

**Review with care. Ship with confidence.**

The Next.js 15 / React 19 interface for Code Turtle: a product page, C# review
workspace, and an illustrative sample with source diffs and inline comments.

![Code Turtle product page](../docs/assets/product-preview.jpg)

## Run locally

Use Node 24.19.0. From the repository root:

```bash
cd web
npm ci
npm run dev
```

Open [localhost:3000](http://localhost:3000) for the product page.

Choose **Try a sample review** or **Explore sample review** to explore authored
review findings against a versioned source/diff fixture. Playback needs no server,
account, access key, or model credentials. It does not run a model or perform live
compilation. The original historical `demo-replay.json` remains unchanged in the
repository and is not used as evidence for this new source fixture.

For a local production preview:

```bash
npm run build
npm start
```

## Review workspace

![Completed illustrative sample in the light Code Turtle workspace](../docs/assets/review-workspace.jpg)

- A GitHub pull request input with URL validation and clearly labeled examples.
- Sample playback with pause, resume, speed selection, skip, and reset controls.
- A findings-first light workspace and compact performance/security/architecture reviewers.
- Separate Review overview, Changes, and Evidence areas with keyboard-accessible tabs.
- Retained/raw findings, severity/persona/keyword filters, and provenance in copied reports.
- Pinned head source, unified diffs, and inline comments with explicit anchor provenance.
- Clear failed-start, interrupted-stream, cancelled, degraded, and unavailable-evidence states.

![Source diff and an authored inline sample comment](../docs/assets/review-diff.jpg)

Live comments anchor to a uniquely resolved compiler-supported member declaration,
not to a model-supplied line number. Uncertain findings remain in the list. Removed
code and non-C# files are labeled outside analysis coverage. GitHub posting and
split diff are not implemented in this increment.

Live reviews require a configured self-hosted backend and trusted repositories.
`NEXT_PUBLIC_API_BASE` selects that backend; if unset, live review requests use
`http://localhost:5000`. Leaving it unset does **not** disable live review controls.
The **Connect your local beta server** disclosure accepts the server's beta access
key, which is held in page memory for the session.
Use the same hostname for both services (for example, `localhost` for both), so the
short-lived session cookie can be used by the browser. Source artifacts are bound
to the creating session, held in memory, and expire with the review job. The UI
shows an explicit unavailable state when an artifact cannot be retrieved.

See [.env.example](.env.example), the [beta guide](../docs/beta/BETA.md), and
[isolation controls](../docs/ISOLATION.md) before enabling the API. A frontend
preview alone does not provide a hosted review service.

## Checks

```bash
npm test
npm run typecheck
npm run build
```

The frontend tests run without model credentials. See the [root README](../README.md)
for backend build commands, verification semantics, and benchmark provenance.

## Screenshots

The README images are actual captures of the local production UI. The review
workspace capture uses the completed illustrative sample. A separate
[mobile capture](../docs/assets/mobile-preview.jpg) shows the product at 390 px.
See the [screenshot inventory](../docs/assets/README.md) for capture states and
refresh instructions.

To regenerate the versioned source/diff sample from deterministic local Git commits:

```bash
node tools/generate_workspace_demo.mjs # run from repository root
```

This regenerates only the two `workspace-*.v1.json` fixtures. It makes no network or
model calls and preserves the historical replay and benchmark artifacts.
