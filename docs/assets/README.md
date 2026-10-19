# Code Turtle screenshots

Current screenshots were captured on 2026-10-19 from the local production web UI.
They use the Code Turtle wordmark, the **Review with care. Ship with confidence.**
tagline, and the forest, ink, paper, sage, and lime palette.

| File | View | Capture state |
|---|---|---|
| [product-preview.jpg](product-preview.jpg) | Full desktop product page | Idle workspace; illustrative hero preview |
| [review-workspace.jpg](review-workspace.jpg) | Desktop review workspace | Completed recorded sample; replay disclosure visible |
| [brand-preview.jpg](brand-preview.jpg) | Full desktop brand library | Primary/reversed logos, palette, typography, usage, and asset links |
| [mobile-preview.jpg](mobile-preview.jpg) | Mobile product page | Top viewport; idle workspace |
| [dashboard-preview.png](dashboard-preview.png) | Historical dashboard | Previous UI; retained for historical document references |

Desktop captures use a 1280 × 900 browser viewport; the mobile capture uses
390 × 844. Browser scrollbars may make the image width slightly narrower.

The review screenshot uses the shipped replay data. It does not represent a fresh
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
   Keep the workspace idle and capture the full page as `product-preview.jpg`.
2. Select **Try a sample review**, then **Skip to End**. Open the **Product**
   anchor and let scrolling and animations settle. Capture the workspace viewport
   as `review-workspace.jpg`, keeping the recorded-sample disclosure visible.
3. Open `/brand` and capture the full page as `brand-preview.jpg`.
4. Return to the idle product page, switch to a 390 × 844 viewport, scroll to the
   top, and capture the visible viewport as `mobile-preview.jpg`.
5. Restore the browser's normal viewport and leave the product page idle.
6. Inspect every image, update the capture date here, and check the image links in
   both the root and web READMEs. Keep the historical PNG labeled as historical.

Use JPEG files for these browser captures. Preserve actual UI copy and sample
disclosures; do not replace them with invented review results or marketing claims.

The source brand assets and usage rules live in
[web/public/brand](../../web/public/brand/brand-guide.md).
