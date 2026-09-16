# Code-Turtle Live — Web Dashboard

Interactive Next.js 15 web client for Code-Turtle-Engine. Features animated turtle persona deliberations and Roslyn-powered Trust Audit verification.

## Deploying to Vercel

Because this repository is structured with the frontend in the `web/` directory and the backend in `.NET`, Vercel needs to know the **Root Directory** for the project:

1. In the [Vercel Dashboard](https://vercel.com/prasad-ranes-projects/code-turtle-engine):
   - Go to **Settings** > **General**.
   - Under **Root Directory**, click **Edit**.
   - Enter or select `web`.
   - Click **Save**.
2. **Environment Variables** (Optional):
   - Under **Settings** > **Environment Variables**, add:
     - `NEXT_PUBLIC_API_BASE`: URL of your deployed `CodeTurtleEngine.Web` backend (e.g., Render, Railway, Fly.io).
     - *Note:* If left blank or unset, the app runs in full **Offline Demo Mode ("Try Demo")** with pre-cached realistic Roslyn audit events.
3. **Trigger Deployment**:
   - Go to the **Deployments** tab and click **Redeploy**, or push a new commit to the repository.

## Local Development

```bash
cd web
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Testing & Build

```bash
npm test        # Run vitest suite
npm run build   # Next.js production build
```
