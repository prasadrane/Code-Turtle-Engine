# Code-Turtle Live — Web Demo Design Spec

**Date:** 2026-09-10
**Lead Architect:** Prasad Sudhir Rane
**Status:** Draft — awaiting review
**Parent project:** Code-Turtle-Engine

---

## 1. Overview

Code-Turtle Live is a portfolio-grade interactive web demo that wraps the Code-Turtle-Engine in a fun, animated browser experience. Users paste a public GitHub PR link and watch a council of animated turtle characters deliberate on the code in real time via Server-Sent Events.

The engine's unique value proposition — **zero-hallucination citation grounding** via Roslyn compilation — is showcased front and center through a live Trust Audit visualization.

### Goals

| Goal | Measure |
|---|---|
| Portfolio showcase | Visitors can experience a full review in < 30 seconds (demo mode) |
| Live PR reviews | Public .NET PRs reviewed with full pipeline, streamed live |
| Fun & memorable | Animated characters, witty copy, personality in every interaction |
| Technical credibility | Trust Audit proves zero-hallucination claim with compiler evidence |

---

## 2. Architecture

```
+------------------------------------------------------------------+
|                   Vercel (Free Tier)                              |
|              Next.js 15 / React 19 / Tailwind CSS                |
|              Framer Motion / Lucide Icons                        |
|                                                                  |
|  +---------------+  +-------------------+  +------------------+ |
|  |  PR Input     |  |  Council Chamber  |  |  Trust Audit     | |
|  |  + Validator  |  |  (SSE-driven)     |  |  Visualizer      | |
|  +------+--------+  +--------^----------+  +--------^---------+ |
|         |                    | SSE Events           |            |
|         |    POST /api/review                       |            |
|         v                    |                      |            |
|  +-------------------------------+------------------+            |
|  |  EventSource -> GET /api/review/{jobId}/stream                |
|  +-------------------------------+-------------------------------+
+----------------------------------+-------------------------------+
                                   | HTTPS (CORS)
+----------------------------------+-------------------------------+
|              Render / Railway / Fly.io (Free Tier)               |
|              ASP.NET Core Minimal API (.NET 10 SDK image)        |
|                                                                  |
|  +--------------+ +---------------+ +-------------------------+ |
|  | GitHub PR    | | Repo Cloner   | | ReviewPipeline          | |
|  | Fetcher      |>| + Restorer    |>| (Gatekeeper>Council>Grd)| |
|  +--------------+ +---------------+ +-------------------------+ |
|                                                                  |
|  +----------------------+  +----------------------------------+  |
|  | SSE Hub              |  | GitHubPrDiffProvider             |  |
|  | (Channel<T> -> SSE)  |  | (IDiffProvider for PR file list) |  |
|  +----------------------+  +----------------------------------+  |
|                                                                  |
|  Env: TURTLE_LLM_BASE_URL, TURTLE_LLM_API_KEY, TURTLE_HOME     |
+------------------------------------------------------------------+
```

### Why this split?

- **Vercel frontend**: Free, global CDN, instant deploys, perfect for Next.js.
- **Container backend**: The engine requires `MSBuildWorkspace` which needs the full .NET 10 SDK, NuGet package restore, and disk access. Cannot run in serverless/edge functions.
- **Communication**: SSE over HTTPS. Simpler than WebSockets, works through proxies, auto-reconnect built into `EventSource` API.

---

## 3. The Turtle Council — Characters

Exactly **3 persona characters** mapped 1:1 to the engine's actual `PersonaRole` enum, plus **1 Arbiter character** for the deterministic merge phase.

### Speedy (lightning)(turtle) — The Performance Freak
- **Engine persona:** `PersonaRole.AllocationsPerformance`
- **Model role:** `"fast"`
- **Personality:** Hyper-caffeinated, talks fast, freaks out about allocations. Carries a tiny stopwatch. Speaks in short bursts.
- **Focus areas:** LOH risk, closures, boxing, unawaited tasks, ConfigureAwait, concurrency.
- **Idle animation:** Tapping foot impatiently, checking stopwatch.
- **Finding animation:** Zips across screen, speech bubble pops with lightning icon.
- **Sample quips:**
  - *"BOXING?! You're allocating on the heap for an INT? My shell is tingling!"*
  - *"This LINQ closure is capturing a local — do you KNOW what the GC has to do?!"*

### Sheldon (lock)(turtle) — The Security Paranoid
- **Engine persona:** `PersonaRole.SecurityAuditor`
- **Model role:** `"deep"`
- **Personality:** Deeply suspicious, wears dark sunglasses, trusts nothing and nobody. Whispers dramatically.
- **Focus areas:** SQL injection, input sanitization, auth bypass, secrets, dependency risks.
- **Idle animation:** Nervously looking left and right, adjusting sunglasses.
- **Finding animation:** Red alert siren on shell, pulls out magnifying glass.
- **Sample quips:**
  - *"String concatenation in a SQL query... I need to sit down."*
  - *"Is that... an API key... IN THE SOURCE CODE?!"*

### Sensei (temple)(turtle) — The Idiomatic Architect
- **Engine persona:** `PersonaRole.IdiomaticArchitect`
- **Model role:** `"deep"`
- **Personality:** Calm zen master, quotes design principles, disappointed-but-kind when code smells.
- **Focus areas:** Modern C#, DI lifetimes, captive dependencies, clean architecture, naming, patterns.
- **Idle animation:** Meditating with floating code symbols around.
- **Finding animation:** Opens a scroll, writes with a brush.
- **Sample quips:**
  - *"A Singleton capturing a Transient... the circle of dependency suffering continues."*
  - *"Your naming tells a story. Sadly, it's a mystery novel."*

### Judge Shellsworth (scales)(turtle) — The Arbiter
- **Not a persona** — represents the deterministic `Arbiter.Merge()` + `TurtleShellGuard.Verify()` steps.
- **Personality:** Stern but fair, wears a tiny judge's wig, carries a gavel. Speaks with authority.
- **Appears when:** All personas have reported. Merges findings, deduplicates, runs the Trust Audit.
- **Animation:** Slams gavel, findings fly in from persona cards, duplicates merge with a satisfying *clink*, unverified citations get stamped with a red X.
- **Sample quips:**
  - *"Order in the chamber! Let me review the evidence..."*
  - *"12 of 14 citations verified by the compiler. 2 hallucinations caught and eliminated. The shell holds."*

---

## 4. User Experience Flow

### 4.1 Landing Page

- Hero section with animated turtle council illustration.
- Large input field: `Paste a public GitHub PR link...`
- Prominent **"Try Demo"** button (pre-cached replay — the default portfolio path).
- Subtle link: "Or paste your own public .NET PR for a live review".
- Example PR chips: clickable links to known good .NET PRs.

### 4.2 PR Validation (Instant — Client + API)

**Client-side** (regex): Validates `https://github.com/{owner}/{repo}/pull/{number}` format.

**Server-side** (GitHub API, ~1-2s):
1. `GET /repos/{owner}/{repo}/pulls/{number}` — if 404/403 -> private/invalid popup.
2. `GET /repos/{owner}/{repo}/pulls/{number}/files` — check file extensions.

### 4.3 Fun Rejection Popups

#### Non-.NET PR Detected
**Trigger:** No `.cs`, `.csproj`, or `.sln` files in the PR's changed file list.

**Animation:** A turtle slowly pokes its head out of its shell, sniffs the code, wrinkles its nose, and retreats back inside with a dramatic shell-slam.

**Message (based on detected language):**

| Detected | Message |
|---|---|
| JavaScript/TypeScript | *"JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!"* |
| Python | *"Indentation-based languages make our turtles dizzy. We only speak curly braces — C# curly braces."* |
| Rust | *"Your borrow checker is impressive, but our turtles haven't learned ownership yet. C# only for now!"* |
| Go | *"`if err != nil` — we felt that. But our turtles only review C# for now!"* |
| Java | *"So close! Same family, different shell. C# only for now!"* |
| Other / Unknown | *"Interesting language! Our turtles are still in C# school. More languages coming soon!"* |

**UI element:** Modal with the turtle animation, the message, a "Coming Soon" badge, and a "Try with a .NET PR instead" button.

#### Private Repository / Invalid PR
**Trigger:** GitHub API returns 404 or 403.

**Animation:** A turtle security guard slides in from the right wearing dark sunglasses, holds up a velvet rope.

**Message:** *"Halt! This repository appears to be private — or this PR doesn't exist. The Turtle Council only reviews public code. No peeking behind private curtains!"*

### 4.4 Live Review Pipeline (SSE-Driven)

Once validated as a public .NET PR, the backend kicks off the pipeline. The frontend displays a **vertical timeline** with animated phases:

```
Phase 1: Fetching the code...
  - Animated turtle with a shopping cart collecting files
  - "Cloning {owner}/{repo} (shallow)..."
  - Duration: ~10-30s

Phase 2: Gathering tools...
  - Turtle putting on a hard hat, running dotnet restore
  - "Restoring NuGet packages..."
  - Duration: ~10-30s

Phase 3: Roslyn is crunching...
  - Turtle feeding code into a machine, gears turning
  - "Compiling with MSBuildWorkspace..."
  - Stats appear: "{N} files analyzed, {M} methods extracted"
  - Duration: ~5-15s

Phase 4: Council is deliberating...
  - THREE persona cards appear simultaneously (parallel)
  - Each card shows the turtle character in "thinking" state
  - As each persona completes, findings pop in one-by-one
  - Cards finish independently at different times
  - If a persona fails: card turns grey, "Speedy got tired"
  - Duration: ~30-280s (this is where the fun is!)

Phase 5: Judge Shellsworth presides...
  - Arbiter turtle appears with gavel
  - Duplicate findings merge with animation
  - Trust Audit runs (see Section 5)
  - Duration: ~1-2s

Phase 6: The Verdict is in!
  - Confetti animation (if clean) or dramatic reveal
  - Final report tabs appear
```

### 4.5 The Council Chamber (Phase 4 Detail)

The main visual during persona execution. Three side-by-side cards:

```
+-----------------+  +-----------------+  +-----------------+
|  Speedy         |  |  Sheldon        |  |  Sensei         |
|                 |  |                 |  |                 |
|  [thinking]     |  |  [thinking]     |  |  [thinking]     |
|  .....          |  |  .....          |  |  .....          |
|                 |  |                 |  |                 |
|  Findings:      |  |  Findings:      |  |  Findings:      |
|  (waiting)      |  |  (waiting)      |  |  (waiting)      |
+-----------------+  +-----------------+  +-----------------+
```

When a persona completes, its card lights up and findings appear with severity badges:
- Red: Critical  | Orange: Error | Yellow: Warning | Blue: Nit | White: Info

Each finding has:
- Title + severity badge
- Location (`file:line`)
- The character's quip/comment (from the LLM's `Detail` field)
- Cited symbols (shown as code chips)

---

## 5. Trust Audit — The Hero Feature

> This is the unique differentiator. Every other AI reviewer hallucinates symbol names.
> Code-Turtle is the ONLY one that compiler-verifies every citation. This must be front and center.

### Trust Audit Visualization

After Judge Shellsworth merges findings, a dedicated "Trust Audit" section animates:

1. **The Scanner**: Animated turtle (Judge Shellsworth) with a magnifying glass examining each cited symbol.

2. **Symbol Verification Table**:
   ```
   +--------------------------------------------------+----------+
   | Cited Symbol (FQN)                               | Verified |
   +--------------------------------------------------+----------+
   | MyApp.Services.IPaymentGateway                   | YES      |
   | MyApp.Services.PaymentService.ChargeAsync         | YES      |
   | System.Linq.Enumerable                           | YES      |
   | MyApp.Services.NonExistentHelper                 | STRIPPED  |
   +--------------------------------------------------+----------+
   ```
   Each row animates in one-by-one. Verified rows get a green glow. Stripped rows get a red strike-through with a satisfying "caught!" animation.

3. **Trust Score Badge**: Large animated badge:
   - `"12/14 citations verified by the Roslyn compiler — 2 hallucinations caught and stripped"`
   - Renders as a shield with a percentage ring

4. **Explainer Tooltip**: On hover, explains:
   > *"Every symbol the AI cites (types, methods, APIs) is checked against what the Roslyn compiler actually resolved in the codebase. If the AI made up a symbol that doesn't exist, the Turtle Shell Guard strips it. This is how we guarantee zero hallucination in citations."*

---

## 6. Demo / Replay Mode

> Essential for portfolio visitors. Most won't have a .NET PR URL handy.

### How It Works

1. Pre-run a real review against the engine's own `SampleRepo` fixture (or a real open-source .NET PR).
2. Capture the full SSE event stream as a JSON array.
3. Store the replay data statically in the Vercel frontend (no backend needed).
4. When user clicks "Try Demo", replay events with realistic timing delays.
5. Full animations, persona cards, Trust Audit — identical experience to a live review.

### Replay Data Shape
```json
[
  { "delay": 0, "event": { "type": "phase", "phase": "cloning", "message": "Cloning SampleRepo..." } },
  { "delay": 2000, "event": { "type": "phase", "phase": "restoring", "message": "Restoring packages..." } },
  { "delay": 4000, "event": { "type": "phase", "phase": "compiling", "stats": { "files": 3, "methods": 8 } } },
  { "delay": 5000, "event": { "type": "persona_started", "persona": "AllocationsPerformance" } },
  { "delay": 5000, "event": { "type": "persona_started", "persona": "SecurityAuditor" } },
  { "delay": 5000, "event": { "type": "persona_started", "persona": "IdiomaticArchitect" } },
  { "delay": 8000, "event": { "type": "persona_finding", "persona": "AllocationsPerformance", "finding": {} } }
]
```

### Benefits
- Zero backend cost for portfolio visitors.
- Instant experience (replay in ~15-25 seconds with accelerated timing).
- Always works, even if the backend container is sleeping.

---

## 7. Backend API Contract

### Endpoints

#### `POST /api/review`
Submit a PR for review.

**Request:**
```json
{
  "prUrl": "https://github.com/owner/repo/pull/123"
}
```

**Response (202 Accepted):**
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "estimatedDurationSeconds": 180
}
```

**Error responses:**
- `400` — Invalid PR URL format
- `422` with `{ "code": "PRIVATE_REPO", "funny": "..." }` — Private/inaccessible repo
- `422` with `{ "code": "NOT_DOTNET", "funny": "...", "detectedLanguage": "python" }` — Non-C# PR
- `429` — Rate limited (max 3 concurrent reviews)
- `503` — Backend overloaded

---

#### `GET /api/review/{jobId}/stream`
SSE event stream for a review job.

**Headers:** `Content-Type: text/event-stream`

**Event schema:**
```
event: phase
data: {"type":"phase","phase":"validating","message":"Checking PR accessibility..."}

event: phase
data: {"type":"phase","phase":"cloning","message":"Cloning owner/repo (shallow)...","progress":0.2}

event: phase
data: {"type":"phase","phase":"restoring","message":"Restoring NuGet packages...","progress":0.4}

event: phase
data: {"type":"phase","phase":"compiling","message":"MSBuild compiling...","progress":0.5,"stats":{"files":12,"methods":47}}

event: persona_started
data: {"type":"persona_started","persona":"AllocationsPerformance","character":"Speedy"}

event: persona_started
data: {"type":"persona_started","persona":"SecurityAuditor","character":"Sheldon"}

event: persona_started
data: {"type":"persona_started","persona":"IdiomaticArchitect","character":"Sensei"}

event: persona_finding
data: {"type":"persona_finding","persona":"SecurityAuditor","character":"Sheldon","finding":{"severity":"Error","title":"SQL Injection Vector","detail":"...","location":"DataAccess.cs:14","citedSymbolFqns":["MyApp.DataAccess.BuildQuery"]}}

event: persona_completed
data: {"type":"persona_completed","persona":"AllocationsPerformance","character":"Speedy","findingCount":3,"quip":"3 allocations found! My stopwatch is crying."}

event: persona_failed
data: {"type":"persona_failed","persona":"IdiomaticArchitect","character":"Sensei","message":"Sensei fell asleep meditating..."}

event: arbiter_merge
data: {"type":"arbiter_merge","character":"Judge Shellsworth","totalRaw":8,"afterDedupe":6,"quip":"Order! 2 duplicate findings merged."}

event: guard_audit
data: {"type":"guard_audit","character":"Judge Shellsworth","verified":10,"stripped":2,"total":12,"auditDetails":[{"citedFqn":"IPaymentGateway","verified":true},{"citedFqn":"NonExistentHelper","verified":false}]}

event: completed
data: {"type":"completed","markdown":"# Code Turtle Review...","verdict":{},"stats":{"duration":142,"personasSucceeded":3,"findingsKept":6,"findingsStripped":1}}

event: error
data: {"type":"error","code":"COMPILATION_FAILED","message":"MSBuild could not compile the project","funny":"The turtles tried their best, but this code won't compile. Even Roslyn gave up."}
```

---

#### `GET /api/review/{jobId}/result`
Fetch completed result (for reconnection after SSE drop).

**Response (200):**
```json
{
  "status": "completed",
  "markdown": "...",
  "verdict": {},
  "stats": {}
}
```

**Response (200, still running):**
```json
{
  "status": "running",
  "currentPhase": "persona_execution",
  "elapsedSeconds": 45
}
```

---

## 8. New Backend Components

### 8.1 `GitHubPrFetcher` (new service)

Calls GitHub REST API (unauthenticated, public repos only):
- Fetches PR metadata: `GET /repos/{owner}/{repo}/pulls/{number}`
- Fetches changed files: `GET /repos/{owner}/{repo}/pulls/{number}/files`
- Extracts: owner, repo, PR number, base SHA, head SHA, head branch, changed file paths
- Detects language by file extensions (.cs -> C#, .py -> Python, etc.)
- Returns structured `PrInfo` record

### 8.2 `GitHubPrDiffProvider : IDiffProvider` (new implementation)

Implements existing `IDiffProvider` interface using the PR's changed file list from GitHub API:

```csharp
public sealed class GitHubPrDiffProvider : IDiffProvider
{
    private readonly IReadOnlyList<string> _changedFiles;

    public GitHubPrDiffProvider(IReadOnlyList<string> changedCsFiles)
        => _changedFiles = changedCsFiles;

    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef)
        => _changedFiles; // Already filtered to .cs files from GitHub API
}
```

This avoids needing LibGit2Sharp to diff the PR — the GitHub API already gives us the changed file list. The cloned repo is only needed for MSBuild compilation.

### 8.3 `RepoCloner` (new service)

Manages temporary shallow clones for web reviews:

```
1. Create temp directory under TURTLE_HOME/web-reviews/{jobId}/
2. git clone --depth 1 --branch {headBranch} https://github.com/{owner}/{repo}.git
3. dotnet restore {discovered .csproj/.sln}
4. Return clone path
5. Cleanup after review completes (or after 10-minute TTL)
```

### 8.4 `ReviewJob` + `ReviewJobRunner` (new background service)

- Jobs stored in a `ConcurrentDictionary<Guid, ReviewJob>`.
- `ReviewJobRunner` is an `IHostedService` processing jobs from a `Channel<ReviewJob>`.
- Each job emits progress events to a `Channel<SseEvent>` that the SSE endpoint reads from.
- Max 3 concurrent reviews (configurable). Queue depth max 5. Beyond that: 429.

### 8.5 `SseHub` (new middleware)

Minimal API endpoint that reads from the job's event channel and writes SSE format:

```csharp
app.MapGet("/api/review/{jobId}/stream", async (Guid jobId, HttpContext ctx, ReviewJobStore store) =>
{
    ctx.Response.ContentType = "text/event-stream";
    var channel = store.GetEventChannel(jobId);
    await foreach (var evt in channel.ReadAllAsync(ctx.RequestAborted))
    {
        await ctx.Response.WriteAsync($"event: {evt.Type}\ndata: {evt.Json}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
});
```

### 8.6 `WebReviewPipeline` (new orchestrator)

Wraps the existing `ReviewPipeline` with SSE event emission at each phase boundary. Does NOT modify the existing pipeline — wraps it:

```
1. Emit "validating"      -> Call GitHubPrFetcher
2. Emit "cloning"          -> Call RepoCloner
3. Emit "restoring"        -> dotnet restore (part of clone step)
4. Emit "compiling"        -> Call existing CompilationLoader
5. Emit "persona_started"  -> Call StreamingPersonaRunner (hooks into individual completions)
6. Emit "arbiter_merge"    -> Call existing Arbiter.Merge()
7. Emit "guard_audit"      -> Call existing TurtleShellGuard.Verify()
8. Emit "completed"        -> Package final result
```

**Key design decision for persona streaming:** The current `PersonaRunner` uses `Task.WhenAll` and returns all results at once. To stream individual persona completions, create a new `StreamingPersonaRunner : IPersonaRunner` that wraps individual `RunOneAsync` tasks with callbacks — keeps the existing `PersonaRunner` untouched.

---

## 9. Frontend Project Structure

```
code-turtle-live/
  src/
    app/
      page.tsx                 # Landing page with PR input
      review/[jobId]/
        page.tsx               # Live review page (SSE consumer)
      demo/
        page.tsx               # Demo replay page
      layout.tsx
    components/
      pr-input.tsx             # PR URL input + validation
      council-chamber.tsx      # 3 persona cards layout
      persona-card.tsx         # Individual persona card with animations
      trust-audit.tsx          # Guard verification visualization
      phase-timeline.tsx       # Vertical progress timeline
      finding-badge.tsx        # Severity badge component
      rejection-popup.tsx      # Funny non-.NET / private popups
      turtle-avatar.tsx        # Animated turtle character
      judge-verdict.tsx        # Arbiter merge animation
      review-report.tsx        # Final markdown report + tabs
    hooks/
      use-sse.ts               # EventSource hook with reconnect
      use-demo-replay.ts       # Replay pre-cached events
    lib/
      characters.ts            # Character metadata and quips
      pr-validator.ts          # Client-side URL regex
      language-detector.ts     # Map file extensions to funny messages
    data/
      demo-replay.json         # Pre-cached demo review events
  public/
    turtles/                   # Turtle character SVGs / Lottie animations
  tailwind.config.ts
  next.config.ts
  package.json
```

---

## 10. Latency Mitigation Strategy

| Concern | Mitigation |
|---|---|
| Cold container start (30-60s) | Demo mode is static/frontend-only; live reviews show "waking up the turtles..." phase |
| Clone + restore (20-60s) | Fun per-phase animations keep users engaged; each phase has its own turtle animation |
| LLM persona calls (30-280s) | Council Chamber shows parallel persona progress; findings stream in as they arrive |
| Total review time (2-5 min) | Clear "estimated time" indicator; progress bar; "grab a coffee" message at 2 min |
| Repo size | Reject repos > 100MB or PRs with > 30 changed .cs files to avoid timeouts |
| SSE disconnection | `GET /api/review/{jobId}/result` endpoint for reconnection; EventSource auto-reconnect |
| Free tier limits | Max 3 concurrent reviews; queue overflow -> friendly "turtles are busy" message |

---

## 11. Deployment

### Frontend (Vercel)
- Next.js 15 app deployed via `vercel` CLI or GitHub integration.
- Environment variable: `NEXT_PUBLIC_API_URL` pointing to the backend.
- Free tier: unlimited static, 100GB bandwidth.

### Backend (Render — Recommended)
- Docker container based on `mcr.microsoft.com/dotnet/sdk:10.0` (SDK, not runtime — needed for MSBuild).
- Free tier: 750 hours/month, auto-sleep after 15 min inactivity.
- Environment variables: `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`, `TURTLE_HOME`, `ALLOWED_ORIGINS`.
- Health check endpoint: `GET /health`.

### Alternative backends
- **Railway**: Free credit/month, no auto-sleep.
- **Fly.io**: 3 free shared-cpu VMs, 256MB RAM (might be tight for large compilations).

---

## 12. Security Constraints

- **No user authentication** — public demo only.
- **No GitHub tokens stored** — unauthenticated GitHub API calls (60 req/hour rate limit per IP).
- **Repo clones are ephemeral** — deleted after review completes or 10-min TTL.
- **No user code is stored** — review results are transient (in-memory only, not persisted to disk on the server).
- **Rate limiting** — max 3 concurrent reviews, max 5 queued, per-IP throttling.
- **CORS** — backend only accepts requests from the Vercel frontend origin.
- **LLM secrets** — never exposed to the frontend; backend env vars only.

---

## 13. Scope Boundaries

### In scope (this spec)
- Vercel Next.js frontend with animations
- ASP.NET Core Minimal API backend with SSE
- `GitHubPrFetcher`, `GitHubPrDiffProvider`, `RepoCloner`
- `WebReviewPipeline` with SSE event emission
- `ReviewJobRunner` background service
- Demo replay mode
- Trust Audit visualization
- Fun rejection popups
- 4 turtle characters (3 personas + 1 Arbiter)

### Out of scope
- GitHub OAuth / private repo access
- Non-C# language support (funny rejection only)
- Persistent review history / database
- User accounts
- CI/CD integration
- Multi-solution / multi-project support per PR
- Mobile-responsive design (desktop-first for portfolio)
