# Code-Turtle-Engine — MVP Design

> **Historical design snapshot (2026-09-08).** As-built reference: [ARCHITECTURE.md](../../../ARCHITECTURE.md); the 60s latency target proved unreachable on the Token Plan relay (measured ~282s, reasoning-bound).

**Date:** 2026-09-08
**Lead Architect:** Prasad Sudhir Rane
**Status:** Approved (design phase) — ready for implementation planning
**Source spec:** `Code-Turtle-Engine Specification.md`

## 1. Overview

Code-Turtle-Engine is an AI pull-request reviewer that grounds large-language-model
reasoning in deterministic compiler truths. Instead of treating code as plain text, it
uses the Roslyn Compiler Platform to build a full-repository compilation, walks the
changed files of a diff to extract structured semantic facts, and feeds those facts —
never raw source — to a deliberative council of AI agents. A deterministic "Turtle Shell"
guard verifies every symbol the agents cite against the compilation, so the system cannot
recommend a method, type, or API that does not exist.

This document scopes the **first build (MVP)** and the iterative roadmap that follows.

### MVP boundary (locked decisions)

| Decision | Choice |
|---|---|
| First-build target | **Core engine + local CLI** (no cloud, no CI yet). Build iteratively. |
| Language / runtime | **.NET 10 LTS** (new project is .NET-based; prior reference projects were Python and are used only as pattern sources, ported idiomatically) |
| Gatekeeper approach | **A — Payload + AST-guard.** `MSBuildWorkspace` full-repo compilation → syntax-walker JSON payload → council → deterministic AST symbol guard. Custom Roslyn analyzers deferred to Phase 2. |
| Roslyn parsing scope | **Full-repo compilation** (clone/open repo, build a complete `Compilation` for cross-file symbol and call-graph resolution) |
| Primary input (v1) | **Manual CLI** — pass a repo path and a diff baseline |
| LLM provider | **Alibaba Bailian "Token Plan"** relay (OpenAI-compatible / DashScope compat-mode), Qwen models, via `Microsoft.Extensions.AI.OpenAI` + Semantic Kernel |

### Success criteria (from spec, MVP-relevant)

- **Zero hallucination:** no finding may cite a method, property, type, or framework API
  that does not resolve in the parsed compilation or referenced metadata.
- **Structured input to the LLM:** the council receives a minified JSON semantic payload,
  not raw source strings.
- **Latency:** end-to-end pipeline target under 60 seconds for a typical PR (full cloud
  path is later; MVP measures CLI wall-clock and records it).
- **Extensibility:** the architecture must keep a clean spine so the Phase-5 MCP server can
  expose at least two AST-query tool endpoints without rework.

Cloud/CI/MCP/GitHub-integration criteria are tracked in the roadmap (Section 11) and are
explicitly out of scope for the MVP.

## 2. Patterns carried from prior projects (ported to idiomatic .NET)

The two reference projects — `Content-Machine` and `CareerGraph-AI` (Operon Job Hunter) —
were Python. Their *conventions* are reused; their *code* is not.

| Prior pattern (Python) | .NET port |
|---|---|
| LLM gateway facade + ordered fallback + mock adapter (CareerGraph `src/core/gateway`, Content-Machine `router/`) | `CodeTurtleEngine.Llm`: `IChatClientFactory` over the Bailian OpenAI-compatible endpoint, ordered fallback chain, `MockChatClient` for offline tests, typed error taxonomy |
| Council loop: parallel persona judges → arbiter synthesis, margin/revision (Content-Machine `council/loop.py`) | `CodeTurtleEngine.Council`: Semantic Kernel personas run with `Task.WhenAll`, then an Arbiter pass |
| Deterministic verification layer (CareerGraph `FactGuard`, Content-Machine verification) | **Turtle Shell guard:** every cited symbol must resolve to a Roslyn `ISymbol` in the compilation, else it is stripped or flagged |
| Secrets-as-env-references in committed config; typed settings; HOME-dir state isolation | `appsettings.json` + `IOptions<TurtleOptions>`; secrets only in env vars referenced by name; artifacts under `TURTLE_HOME` |
| Frozen versioned rubric (`council/rubric_v1.md`) | `turtle/rubric_v1.md` review criteria, treated as data not code |
| Offline-first tests, 1:1 subsystem↔test-file naming, live smoke gated by env var; `spikes/` + `RESULTS.md`; `docs/superpowers/{specs,plans}`; `NOTES.md` build log | xUnit + NSubstitute, same layout and conventions |
| ≤300-lines-per-file invariant | same |
| Pure `classify_status()` for HTTP/error mapping (Content-Machine) | pure `ClassifyStatus()` static, unit-tested without network |

## 3. Solution and project layout

Solution: `CodeTurtleEngine.sln` targeting `net10.0`.

```
src/
  CodeTurtleEngine.Core/         # domain records & DTOs; no external deps
                                 #   RoslynPayload, MethodFacts, ReviewFinding, CouncilVerdict, enums
  CodeTurtleEngine.Gatekeeper/   # Roslyn: MSBuildWorkspace loader, TurtleSyntaxWalker,
                                 #   SemanticModel analysis, payload generator, DiffProvider (LibGit2Sharp)
  CodeTurtleEngine.Llm/          # gateway: IChatClientFactory (Microsoft.Extensions.AI.OpenAI),
                                 #   Bailian/Qwen config, ordered fallback, MockChatClient, error taxonomy, Polly resilience
  CodeTurtleEngine.Council/      # Semantic Kernel personas + Arbiter + TurtleShellGuard + rubric loader
  CodeTurtleEngine.Cli/          # System.CommandLine entrypoint: `turtle review <repo> [--diff <ref>] [--out <file>] [--json]`
tests/
  CodeTurtleEngine.Core.Tests/
  CodeTurtleEngine.Gatekeeper.Tests/
  CodeTurtleEngine.Llm.Tests/
  CodeTurtleEngine.Council.Tests/
  CodeTurtleEngine.Cli.Tests/
  fixtures/SampleRepo/           # small buildable repo with seeded defects for gatekeeper/e2e tests
spikes/                          # throwaway capability probes + RESULTS.md
turtle/rubric_v1.md              # frozen review rubric
docs/superpowers/specs/          # this document
docs/superpowers/plans/          # implementation plan (next step)
NOTES.md                         # chronological build/spike log
AGENTS.md                        # agent-ops contract (architecture map, commands, invariants)
```

**Dependency flow:** `Cli → {Council, Gatekeeper, Llm} → Core`; `Council → Gatekeeper`
(the guard needs the live `Compilation`/`ISymbol`s). `Core` has no project dependencies.

**File-size invariant:** keep each source file ≤300 lines; split when a file grows past that.

## 4. Data flow (CLI pipeline)

The pipeline **fails closed**: without a successful compilation there is no payload, and
without a payload there is no review.

```
1. Parse args            repoPath, diff baseline (default working-tree vs HEAD, or base..head ref), outPath, format
2. DiffProvider          changed *.cs files + hunks (LibGit2Sharp)
3. Gatekeeper.Compile    MSBuildWorkspace.OpenProjectAsync/Solution → GetCompilationAsync() = full-repo symbols
                         (compilation failure ⇒ CompilationException ⇒ abort, report build errors)
4. Gatekeeper.Walk       TurtleSyntaxWalker over changed SyntaxTrees + SemanticModel analysis
5. Gatekeeper.Payload    minified JSON semantic payload (per method/file)
6. Council.Run           3 personas in parallel (Task.WhenAll) over payload + rubric → typed CouncilVerdict
7. Arbiter.Synthesize    dedupe overlaps, resolve conflicts (perf vs readability), unified markdown review
8. TurtleShellGuard      verify every cited symbol FQN against the Compilation; strip/flag unresolved
9. Output                write review.md (+ payload.json, verdicts.json, audit.json artifacts); set exit code
```

## 5. Gatekeeper (Roslyn) details

`TurtleSyntaxWalker : CSharpSyntaxWalker` visits the changed files' syntax trees and
collects nodes including `MethodDeclarationSyntax`, `InvocationExpressionSyntax`,
`AwaitExpressionSyntax`, `CastExpressionSyntax`, LINQ query/extension expressions,
`ObjectCreationExpressionSyntax`, and DI registration calls.

Using the `SemanticModel`, the gatekeeper resolves `ISymbol`s and detects, deterministically:

- missing `ConfigureAwait(false)` on awaited tasks
- implicit boxing operations
- unawaited tasks (fire-and-forget)
- potentially dangerous casts
- captive dependencies (e.g., a Transient service captured by a Singleton)
- call-graph edges from invocation sites

**Payload schema** (illustrative; matches the source spec example, extended with the
guard's source of truth):

```json
{
  "Method": "ProcessPaymentAsync",
  "Allocations": ["LINQ Closure (Line 42)", "Implicit Boxing (Line 45)"],
  "AsyncHealth": "Missing ConfigureAwait(false) on IHttpClientFactory invocation",
  "Dependencies": ["IPaymentGateway", "ILogger"],
  "ResolvedSymbols": ["MyApp.IPaymentGateway", "System.Net.Http.IHttpClientFactory"]
}
```

`ResolvedSymbols` carries full Roslyn display strings / metadata names. These are the
allow-list the Turtle Shell guard checks agent citations against. **Raw source is never
sent to the LLM** — only this minified payload — to preserve context-window tokens and to
give the model structured data rather than text (a hard spec requirement).

**Compilation loader:** primary path is `Microsoft.CodeAnalysis.Workspaces.MSBuild`
(`MSBuildWorkspace`), which requires the .NET SDK and a NuGet restore of the target repo.
**Risk:** `MSBuildWorkspace` behavior on .NET 10 must be validated; if it is problematic,
the fallback is `Buildalyzer` (drives `dotnet build` with an MSBuild logger to capture the
compilation). A spike (Section 10) settles this before the gatekeeper is built.

## 6. Council and LLM gateway

### Personas (from spec)

- **Allocations & Performance Expert** — memory leaks, large-object-heap risk, unawaited
  tasks, thread-safety in concurrent collections.
- **Security Auditor** — input sanitization, SQL-injection vectors (raw string
  concatenation bypassing EF Core parameters), authentication-bypass risk on REST/GraphQL
  endpoints.
- **Idiomatic Architect** — modern C# language features, clean-architecture boundaries,
  correct DI lifecycles.
- **Arbiter** — final synthesis node: evaluates the council's verdicts, discards overlapping
  nitpicks, resolves conflicts (performance vs readability), and produces a unified
  markdown review.

The three reviewing personas run in parallel (`Task.WhenAll`); the Arbiter runs once after.

### Gateway (`CodeTurtleEngine.Llm`)

- `OpenAIChatClient` (from `Microsoft.Extensions.AI.OpenAI`) pointed at the Bailian
  OpenAI-compatible base URL with the API key from environment.
- **Model role map:** `deep = qwen3.8-max` (Arbiter, Security Auditor, Idiomatic Architect);
  `fast = qwen3.8-flash` (Allocations & Performance scan / cheap passes). Role→model mapping
  lives in config, not code.
- **Ordered fallback chain:** primary route then secondary; if all routes fail, throw
  `ProviderException` carrying the full attempt log.
- **Structured output:** request a JSON schema and parse to a typed `CouncilVerdict` via
  `System.Text.Json`. **Degrade path** (because Qwen compat-mode schema support is partial):
  fall back to explicit-JSON prompting plus mandatory local validation, mirroring the
  Content-Machine `messages_adapter` approach.
- **Resilience:** Polly v8 retry + timeout + circuit breaker around each call.
- **`MockChatClient`:** returns canned verdicts so the entire council is testable offline.

## 7. Turtle Shell guard (zero-hallucination enforcement)

After the council produces verdicts, the guard parses each finding's cited symbol FQN and
attempts to resolve it against the live `Compilation`'s symbol set and referenced framework
metadata:

- **Resolved** → finding kept, marked `[verified]`.
- **Unresolved** → finding stripped, or marked `[unverified]` (configurable). This removes
  hallucinated methods, types, and BCL/framework APIs.

The guard writes an `audit.json` recording every citation and its verification result, so
the zero-hallucination guarantee is observable and testable.

## 8. Configuration and secrets

- `appsettings.json` (committed, **never contains secrets**): routes referencing env vars
  **by name** (`base_url_env`, `api_key_env`), model role map, thresholds (severity cutoff,
  quorum, max revision iterations), rubric path, artifact directory, guard mode
  (strip vs flag).
- Environment variables: `TURTLE_LLM_BASE_URL` (Bailian compatible-mode endpoint),
  `TURTLE_LLM_API_KEY`, `TURTLE_HOME` (artifact root, default `~/.turtle`).
- Strongly-typed `IOptions<TurtleOptions>` bound at startup. Secrets resolve from env at
  client construction, matching the prior projects' secrets-as-env-references convention.

## 9. Error handling and resilience

- Typed exceptions: `ProviderException`, `ModelException`, `CompilationException`,
  `DiffException`, `GuardException`.
- Pure static `ClassifyStatus(httpStatus, errorBody)` maps transport/LLM errors to the
  taxonomy — unit-tested without any network.
- The CLI catches top-level exceptions, writes a human-readable message to stderr, and
  returns a non-zero exit code.
- **Fail-closed compilation:** if the target repo does not compile, abort before the council
  (no payload ⇒ no grounding ⇒ no review).
- **Partial council failure:** if a persona still fails after the fallback chain, proceed with
  the remaining personas when the configured quorum is met, and note the degraded review;
  otherwise fail.

## 10. Persistence (MVP)

Artifacts are written under `TURTLE_HOME/reviews/{repoSlug}/{timestamp}/`:

- `payload.json` — the Roslyn semantic payload sent to the council
- `verdicts.json` — per-persona raw verdicts
- `review.md` — the Arbiter's unified markdown review
- `audit.json` — Turtle Shell guard verification results

JSON files only for the MVP. A SQLite review-history/audit store (Content-Machine
`storage/db.py` pattern) is deferred to Phase 3.

## 11. Testing strategy

- xUnit + NSubstitute, **offline-first**; 1:1 subsystem↔test-file naming.
- **Gatekeeper tests:** build `tests/fixtures/SampleRepo` (a small compilable repo seeded
  with known defects — missing `ConfigureAwait(false)`, boxing, an unawaited task, a captive
  DI dependency) and assert the payload contents and symbol resolution.
- **Council tests:** drive with `MockChatClient` canned verdicts; assert parallel
  orchestration, dedupe, quorum handling, and that the guard strips unresolved citations.
- **Llm tests:** `ClassifyStatus` cases, fallback-chain behavior, mock client.
- **Guard tests:** a hallucinated symbol citation is stripped/flagged.
- **CLI tests:** end-to-end offline against the fixture repo with `MockChatClient`, asserting
  a `review.md` is produced and exit codes are correct.
- **Live smoke:** gated by `TURTLE_LIVE=1`, exercises the real Bailian endpoint; kept out of
  the default offline suite.

### Spikes (before/at start of implementation)

Recorded in `spikes/` with results in `spikes/RESULTS.md`:

1. `MSBuildWorkspace` (vs `Buildalyzer`) opening a real repo and producing a `Compilation` on
   .NET 10 — timing and correctness.
2. Bailian/Qwen OpenAI-compatible structured-output support — confirm schema mode vs the
   degrade path.

## 12. Iterative roadmap (post-MVP)

| Phase | Scope |
|---|---|
| **P2** | Custom Roslyn `DiagnosticAnalyzer`s (Approach C backbone): deterministic findings the LLM only enriches/explains/prioritizes |
| **P3** | SQLite review-history + audit persistence and trending |
| **P4** | GitHub Actions integration + Octokit: run on a PR and post the review as a comment |
| **P5** | MCP server (`ModelContextProtocol.NET`) exposing ≥2 AST-query tool endpoints to IDEs / Claude Code |
| **P6** | ASP.NET Core webhook receiver (real-time PR events) |
| **P7** | Docker + AWS ECS Fargate + Terraform (ECR, ECS, IAM for Bedrock/LLM) + GitHub Actions CI/CD |

## 13. Out of scope for MVP

Cloud hosting (AWS/Fargate/Terraform), Docker, the ASP.NET webhook API, GitHub
Actions/Octokit integration, and the MCP server. These are roadmap phases P4–P7 and are
deliberately excluded from the first build, which is a local CLI proving the
gatekeeper → council → guard → review pipeline end to end.
