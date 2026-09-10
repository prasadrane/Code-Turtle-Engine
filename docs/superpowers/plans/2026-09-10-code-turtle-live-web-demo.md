# Code-Turtle Live — Web Demo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portfolio-grade web demo for Code-Turtle-Engine featuring an interactive Next.js 15 frontend on Vercel with animated turtle personas and Trust Audit visualizer, powered by an ASP.NET Core Minimal API backend streaming real-time pipeline events via SSE.

**Architecture:** A lightweight ASP.NET Core service (`CodeTurtleEngine.Web`) wraps the core review pipeline with GitHub PR extraction, shallow repository cloning, and asynchronous Server-Sent Events (SSE). A Next.js 15 frontend provides animated council interactions, fast language rejection modals, and a pre-cached offline demo replay mode.

**Tech Stack:** 
- Backend: .NET 10 LTS, ASP.NET Core Minimal APIs, System.Threading.Channels, Roslyn, LibGit2Sharp, xUnit.
- Frontend: Next.js 15 (App Router), React 19, TypeScript, Tailwind CSS, Framer Motion, Lucide React.

## Global Constraints

- Backend adheres strictly to `≤300 lines per source file`.
- Fail closed: non-.NET PRs rejected immediately without cloning.
- Secrets never stored in code or repository (only env vars: `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`, `TURTLE_HOME`).
- TDD: failing tests first, verify red, implement minimal code, verify green, commit per task.
- Zero-hallucination verification must remain untrimmed in the background and auditable.

---

## Phase 1: Backend Core Services & Models (TDD)

### Task 1: Web Project Setup & PR URL Parsing & Language Detector

**Files:**
- Create: `src/CodeTurtleEngine.Web/CodeTurtleEngine.Web.csproj`
- Create: `src/CodeTurtleEngine.Web/Models/PrModels.cs`
- Create: `tests/CodeTurtleEngine.Web.Tests/CodeTurtleEngine.Web.Tests.csproj`
- Create: `tests/CodeTurtleEngine.Web.Tests/PrModelsTests.cs`

**Interfaces:**
- Consumes: Standard .NET primitives
- Produces: 
  ```csharp
  public record GitHubPrRequest(string PrUrl);
  public record ParsedPrUrl(string Owner, string Repo, int PullNumber);
  public static class PrUrlParser { public static bool TryParse(string url, out ParsedPrUrl? result); }
  public static class LanguageDetector { public static (bool IsDotNet, string PrimaryLanguage, string FunnyMessage) Inspect(IEnumerable<string> filePaths); }
  ```

- [ ] **Step 1: Write failing tests for PR URL parsing and language detection**
- [ ] **Step 2: Run tests to verify failure** (`dotnet test tests/CodeTurtleEngine.Web.Tests`)
- [ ] **Step 3: Implement `PrModels.cs` with regex URL parsing and extension-based language detection**
- [ ] **Step 4: Run tests to verify green**
- [ ] **Step 5: Commit task** (`git commit -m "feat(web): add PrUrlParser and LanguageDetector with unit tests"`)

---

### Task 2: GitHub PR Diff Provider (`IDiffProvider`)

**Files:**
- Create: `src/CodeTurtleEngine.Web/Services/GitHubPrDiffProvider.cs`
- Create: `tests/CodeTurtleEngine.Web.Tests/GitHubPrDiffProviderTests.cs`

**Interfaces:**
- Consumes: `CodeTurtleEngine.Gatekeeper.IDiffProvider`
- Produces:
  ```csharp
  public sealed class GitHubPrDiffProvider : IDiffProvider
  {
      public GitHubPrDiffProvider(IReadOnlyList<string> changedFilePaths);
      public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef);
  }
  ```

- [ ] **Step 1: Write failing test verifying filtering of `.cs` files and case-insensitive deduplication**
- [ ] **Step 2: Run test to verify failure**
- [ ] **Step 3: Implement `GitHubPrDiffProvider`**
- [ ] **Step 4: Run test to verify green**
- [ ] **Step 5: Commit task** (`git commit -m "feat(web): implement GitHubPrDiffProvider for PR file lists"`)

---

### Task 3: SSE Events & ReviewJobStore

**Files:**
- Create: `src/CodeTurtleEngine.Web/Models/SseEvents.cs`
- Create: `src/CodeTurtleEngine.Web/Services/ReviewJobStore.cs`
- Create: `tests/CodeTurtleEngine.Web.Tests/ReviewJobStoreTests.cs`

**Interfaces:**
- Consumes: `CodeTurtleEngine.Core` models
- Produces:
  ```csharp
  public record SseEvent(string Type, object Data);
  public sealed class ReviewJobStore
  {
      public Guid CreateJob(string prUrl);
      public System.Threading.Channels.ChannelWriter<SseEvent> GetWriter(Guid jobId);
      public System.Threading.Channels.ChannelReader<SseEvent> GetReader(Guid jobId);
      public void SetResult(Guid jobId, CodeTurtleEngine.Cli.ReviewResult result);
      public bool TryGetResult(Guid jobId, out CodeTurtleEngine.Cli.ReviewResult? result);
  }
  ```

- [ ] **Step 1: Write unit tests for creating jobs, streaming events via `Channel<SseEvent>`, and storing completion results**
- [ ] **Step 2: Run test to verify failure**
- [ ] **Step 3: Implement `SseEvents.cs` and `ReviewJobStore.cs` with `System.Threading.Channels`**
- [ ] **Step 4: Run test to verify green**
- [ ] **Step 5: Commit task** (`git commit -m "feat(web): add SseEvents model and thread-safe ReviewJobStore"`)

---

### Task 4: Streaming Persona Runner

**Files:**
- Create: `src/CodeTurtleEngine.Web/Services/StreamingPersonaRunner.cs`
- Create: `tests/CodeTurtleEngine.Web.Tests/StreamingPersonaRunnerTests.cs`

**Interfaces:**
- Consumes: `IPersonaRunner`, `IStructuredChatClient`, `CouncilOptions`
- Produces:
  ```csharp
  public interface IStreamingPersonaRunner
  {
      Task<IReadOnlyList<PersonaVerdict>> RunWithCallbacksAsync(
          RoslynPayload payload,
          string rubric,
          Func<PersonaRole, Task> onStarted,
          Func<PersonaVerdict, Task> onCompleted,
          Func<PersonaRole, string, Task> onFailed,
          CancellationToken ct = default);
  }
  ```

- [ ] **Step 1: Write unit tests using `MockChatClient` verifying callbacks fire as each persona starts and completes**
- [ ] **Step 2: Run test to verify failure**
- [ ] **Step 3: Implement `StreamingPersonaRunner` wrapping the 3 parallel persona calls with callback hooks**
- [ ] **Step 4: Run test to verify green**
- [ ] **Step 5: Commit task** (`git commit -m "feat(web): implement StreamingPersonaRunner with per-persona progress callbacks"`)

---

## Phase 2: Web API & Pipeline Orchestration

### Task 5: WebReviewPipeline & Minimal API Endpoints

**Files:**
- Create: `src/CodeTurtleEngine.Web/Services/WebReviewPipeline.cs`
- Create: `src/CodeTurtleEngine.Web/Program.cs`
- Create: `tests/CodeTurtleEngine.Web.Tests/ApiEndpointsTests.cs`

**Interfaces:**
- Endpoints:
  - `POST /api/review` -> `{ jobId, estimatedDurationSeconds }`
  - `GET /api/review/{jobId}/stream` -> `text/event-stream`
  - `GET /api/review/{jobId}/result` -> JSON `ReviewResult`
  - `GET /health` -> `200 OK`

- [ ] **Step 1: Write integration tests using `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory` for all endpoints**
- [ ] **Step 2: Run test to verify failure**
- [ ] **Step 3: Implement `WebReviewPipeline` and wire endpoints in `Program.cs` with CORS and error handling**
- [ ] **Step 4: Run test to verify green**
- [ ] **Step 5: Commit task** (`git commit -m "feat(web): add WebReviewPipeline and ASP.NET Core Minimal API endpoints"`)

---

## Phase 3: Next.js Frontend Dashboard

### Task 6: Next.js 15 Project Setup & TypeScript Types & Demo Replay Data

**Files:**
- Create: `web/package.json`
- Create: `web/src/lib/types.ts`
- Create: `web/src/lib/characters.ts`
- Create: `web/src/data/demo-replay.json`
- Create: `web/src/hooks/useDemoReplay.ts`

- [ ] **Step 1: Initialize Next.js project with Tailwind CSS and Lucide icons**
- [ ] **Step 2: Define TypeScript interfaces matching backend SSE schemas**
- [ ] **Step 3: Capture and format realistic demo review events in `demo-replay.json` (seeded from `SampleRepo`)**
- [ ] **Step 4: Implement `useDemoReplay` state machine hook**
- [ ] **Step 5: Commit task** (`git commit -m "feat(frontend): scaffold Next.js app with types, characters, and demo replay hook"`)

---

### Task 7: Council Chamber & Animated Persona Cards

**Files:**
- Create: `web/src/components/CouncilChamber.tsx`
- Create: `web/src/components/PersonaCard.tsx`
- Create: `web/src/components/TurtleAvatar.tsx`

**Features:**
- Live animated cards for Speedy ⚡, Sheldon 🔒, and Sensei 🏯
- Thinking spinners, state transitions, speech bubbles, and pop-in findings with severity badges.

- [ ] **Step 1: Build `TurtleAvatar` with SVG illustrations and mood animations (idle, thinking, alert, celebrating)**
- [ ] **Step 2: Build `PersonaCard` with speech bubble quips, finding counts, and animated findings list**
- [ ] **Step 3: Build `CouncilChamber` displaying the 3 cards side-by-side**
- [ ] **Step 4: Verify visually and verify unit rendering**
- [ ] **Step 5: Commit task** (`git commit -m "feat(frontend): add animated Council Chamber and Persona Cards"`)

---

### Task 8: Hero Feature: Trust Audit Visualizer & Rejection Popups

**Files:**
- Create: `web/src/components/TrustAuditVisualizer.tsx`
- Create: `web/src/components/LanguageRejectionModal.tsx`
- Create: `web/src/components/PhaseTimeline.tsx`
- Create: `web/src/components/FinalReportView.tsx`

**Features:**
- Judge Shellsworth ⚖️ presiding with gavel animation
- Symbol verification scanner with ✅ Verified and ❌ Hallucination Stripped badges
- Funny non-.NET modal with retreating turtle animation
- Comprehensive final review markdown tab view

- [ ] **Step 1: Build `LanguageRejectionModal` with witty language messages and retry actions**
- [ ] **Step 2: Build `TrustAuditVisualizer` featuring animated symbol citation checks and Trust Score shield**
- [ ] **Step 3: Build `PhaseTimeline` showing real-time pipeline stages**
- [ ] **Step 4: Build `FinalReportView` rendering synthesized review markdown and metrics**
- [ ] **Step 5: Commit task** (`git commit -m "feat(frontend): add Trust Audit visualizer, rejection modals, and final report view"`)

---

### Task 9: Landing Page Integration & End-to-End Verification

**Files:**
- Modify: `web/src/app/page.tsx`
- Create: `web/src/components/PrInput.tsx`
- Create: `web/src/hooks/useReviewStream.ts`

- [ ] **Step 1: Implement `PrInput` with URL validation and "Try Demo" preset trigger**
- [ ] **Step 2: Integrate `useReviewStream` (live SSE) and `useDemoReplay` (offline demo) into `page.tsx`**
- [ ] **Step 3: Test demo flow end-to-end (instant portfolio visitor path)**
- [ ] **Step 4: Test live API integration against local backend**
- [ ] **Step 5: Run full test suites (`dotnet test` across all backend projects)**
- [ ] **Step 6: Commit task** (`git commit -m "feat(web): wire full interactive landing page with SSE and demo replay"`)
