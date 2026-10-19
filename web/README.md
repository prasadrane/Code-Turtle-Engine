<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="public/brand/code-turtle-logo-reversed.svg">
    <img src="public/brand/code-turtle-logo.svg" alt="Code Turtle" width="300">
  </picture>
</p>

# Code Turtle · Web UI

**Review with care. Ship with confidence.**

The Next.js 15 / React 19 interface for Code Turtle: a product page, C# review
workspace, and recorded sample replay.

![Code Turtle product page](../docs/assets/product-preview.jpg)

## Run locally

Use Node 24.19.0. From the repository root:

```bash
cd web
npm ci
npm run dev
```

Open [localhost:3000](http://localhost:3000) for the product page.

Choose **Try a sample review** or **Watch the sample** to explore the recorded
review. The replay needs no server, account, access key, or model credentials.
It is historical sample data, not a fresh model run.

For a local production preview:

```bash
npm run build
npm start
```

## Review workspace

![Completed Code Turtle sample review with the dark forest console](../docs/assets/review-workspace.jpg)

- A GitHub pull request input with URL validation and clearly labeled examples.
- Sample playback with pause, resume, speed selection, skip, and reset controls.
- Performance, security, and architecture personas with provisional findings.
- Citation evidence, severity/persona/keyword filters, and a final report.

Live reviews require a configured self-hosted backend and trusted repositories.
`NEXT_PUBLIC_API_BASE` selects that backend; if unset, live review requests use
`http://localhost:5000`. Leaving it unset does **not** disable live review controls.
The **Connect your local beta server** disclosure accepts the server's beta access
key, which is held in page memory for the session.

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
workspace capture uses the completed recorded sample. A separate
[mobile capture](../docs/assets/mobile-preview.jpg) shows the product at 390 px.
See the [screenshot inventory](../docs/assets/README.md) for capture states and
refresh instructions.
