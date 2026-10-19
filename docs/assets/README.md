# Code Turtle screenshots

Current screenshots were captured on 2026-10-19 from the local production web UI.
They use the Code Turtle wordmark, the **Review with care. Ship with confidence.**
tagline, and the forest, ink, paper, sage, and lime palette.

| File | View | Capture state |
|---|---|---|
| [product-preview.jpg](product-preview.jpg) | Desktop product top viewport | Idle workspace; illustrative hero preview |
| [review-workspace.jpg](review-workspace.jpg) | Desktop review workspace | Completed illustrative sample; findings and disclosure visible |
| [review-diff.jpg](review-diff.jpg) | Desktop Changes tab | Pinned fixture diff, added/removed lines, authored inline comment |
| [mobile-preview.jpg](mobile-preview.jpg) | Mobile product page | Top viewport; idle workspace |
| [dashboard-preview.png](dashboard-preview.png) | Historical dashboard | Previous UI; retained for historical document references |

Desktop captures use a 1280 × 900 browser viewport; the mobile capture uses
390 × 844. Browser chrome and scrollbars may make the image dimensions slightly smaller.

The review screenshots use the versioned illustrative fixture. They do not represent a fresh
model review, new benchmark result, or hosted-service validation. No server access
key or model credential is included in these captures.

## Refresh the screenshots

From the repository root, build and serve the frontend:

```bash
cd web
npm ci
npm run build
npm start
```

1. Open the product page at `http://localhost:3000` in a 1280 × 900 viewport.
   Keep the workspace idle, scroll to the top, and capture the visible viewport
   as `product-preview.jpg`.
2. Select **Try a sample review**, then **Skip to End** if playback is still active.
   Scroll to the working workspace. Assert **Complete**, the illustrative-sample
   disclosure, retained findings, and review controls are visible. Capture that
   viewport as `review-workspace.jpg`. Do not capture the hero illustration here.
3. Select **View code context** on the customer-query finding. Assert the Changes
   tab, pinned revision, added/removed SQL lines, and authored inline comment are
   visible. Capture the viewport as `review-diff.jpg`.
4. Return to the idle product page, switch to a 390 × 844 viewport, scroll to the
   top, and capture the visible viewport as `mobile-preview.jpg`.
5. Restore the browser's normal viewport and leave the product page idle.
6. Open and inspect every saved image to confirm the documented state, update the
   capture date here, and check the image links in
   both the root and web READMEs. Keep the historical PNG labeled as historical.

Use JPEG files for these browser captures. Preserve actual UI copy and sample
disclosures; do not replace them with invented review results or marketing claims.
