# NOTES — Code-Turtle-Engine build log (compressed)

## Status: MVP COMPLETE (2026-09-08) — Tasks 1, 2, 4–17 done; Task 3 creds-deferred
Branch `feat/mvp-implementation` (20 commits over master). Offline suite green (45/45); full build 0W/0E. E2E proves core value path: real Roslyn compilation grounds council, Turtle Shell guard strips hallucinated citation.
Final whole-branch review (opus, 16b1f18..9aa62d6): Ready-to-merge WITH FIXES, NO Critical — architecture verified sound (zero-hallucination citation chain no-bypass, fail-closed, secrets clean, 8 cross-task handoffs line up, tests real). Pre-merge fix wave applied (990226a): pipeline strip-assertion test, README cited-symbol precision + single-project scope, NOTES path, ArtifactWriter InvariantCulture+ticks, CLI latency recording, degraded-review banner. Scoped re-review: all fixes ADDRESSED, no new Critical/Important. Branch ready to integrate.

## Done (task — commit — what)
- T1 scaffold (83bb258): sln + Directory.Build.props (net10, TreatWarningsAsErrors) + global.json; 5 src + 6 test projects; refs Cli->{Council,Gatekeeper,Llm,Core}, Council->{Core,Gatekeeper,Llm}.
- T2 Spike A (24c6f48): MSBuildWorkspace OK on net10 (Roslyn 5.9.0, ~3s warm). DECISION: T10 loader = MSBuildWorkspace, not Buildalyzer. Evidence `spikes/RESULTS.md`.
- T4 fixture (470210f): `tests/fixtures/SampleRepo` — PaymentService/DataAccess/DiRegistration/ILogger/IPaymentGateway; 7 FQNs + 6 seeded defects.
- T5 Core (095a29f): records RoslynPayload/FileFacts/MethodFacts/ReviewFinding/PersonaVerdict/GuardResult/CouncilVerdict + enums Severity/PersonaRole/GuardMode + TurtleJson (string-enum converter).
- T6 (a2b4f29): ErrorClassifier (HTTP->LlmErrorKind) + ProviderException/ModelException.
- T7 (b29cbf5): BailianChatClientFactory (`OpenAIClient.GetChatClient(model).AsIChatClient()`) + MockChatClient (real IChatClient).
- T8 (c680ede): LlmGateway ordered fallback + Polly retry/timeout/breaker; StructuredChatClient schema-primary + degrade (ExtractJson). Both paths ship -> Spike B non-blocking.
- T9 (504047c): DiffProvider (LibGit2Sharp 0.32) working-tree + baselineRef tree-vs-tree; .cs filter; DiffException.
- T10 (6f2d74c): MsBuildCompilationLoader, `LoadedCompilation(Compilation, IDisposable Workspace)`, CompilationException. Fail closed.
- T11 (b7d1baf): TurtleSyntaxWalker + SemanticAnalyzer — ConfigureAwait/boxing/LINQ-closure/ctor-deps/ResolvedSymbols. Detections generic, no fixture hardcoding.
- T12 (1adaaaa): PayloadGenerator.Build(slug, baseline, changed) -> RoslynPayload; relative paths; PathsMatch EndsWith.
- T13 (3d1f87d): RubricLoader, PromptBuilder (persona+payload+grounding rules), VerdictSchema, VerdictDto/FindingDto, CouncilOptions{Quorum=2,Guard=Strip}.
- T14 (02da47a): PersonaRunner parallel, per-persona try/catch->null, quorum gate, ModelRoleFor (Allocations->fast else deep); Arbiter deterministic Merge (dedupe title+location keep-highest severity, order) + RenderMarkdown.
- T15 (9d6dd29): TurtleShellGuard — Ordinal exact allow-list from payload ResolvedSymbols; audit every cite pre-mode; Strip drops zero-verified findings, reduces mixed; Flag keeps.
- T16 (3938d49): CLI `review` (System.CommandLine 3.0.0-rc.1), ReviewPipeline diff->compile->payload->council->guard->markdown, ArtifactWriter 4 artifacts to $TURTLE_HOME/reviews/{slug}/{timestamp}/, Composition DI, appsettings env-NAMES-only.
- T17 (this commit): e2e integration test (offline, CitingChat mock cites real payload symbol + fake; guard strips fake), LiveSmokeTests (self-skip unless TURTLE_LIVE=1), docs (README/AGENTS/rubric_v1), deleted 5 template UnitTest1.cs placeholders.

## Deferred / gated
- T3 Spike B (Bailian structured-output probe) + live smoke run: creds `TURTLE_LLM_BASE_URL`/`TURTLE_LLM_API_KEY` set on machine but not visible to tool shell. Unblock: ~/.bashrc exports or setx + restart. StructuredChat already ships degrade path.
- Phase 2 (per rulings, documented in README "MVP detection scope"): custom Roslyn analyzers for dangerous-cast / unawaited-task / captive-DI detection; LLM-based Arbiter conflict resolution; cloud/CI/GitHub/MCP; per-route circuit breakers; OCE propagation in PersonaRunner/guard pipelines.
- Flag-mode `[verified]`/`[unverified]` visible markers (spec §7): Phase 2 — Strip is the MVP default.
- `GuardException` type not created: guard filters citations rather than throwing, by design.
- Multi-project solution compilation: Phase 2 — single-project MVP scope, now documented in README.
- IMPLEMENTED in pre-merge fix wave: CLI latency recording (stderr `elapsed: N ms`, spec success-criterion-3); degraded-review banner in ReviewPipeline when <3 council personas succeed (spec §9); ArtifactWriter InvariantCulture + ticks-uniqued timestamp dir.

## Key API adaptations (compiler-driven, verified)
- MEAI 10.9.0: schema = `ChatResponseFormat.ForJsonSchema(JsonDocument.Parse(jsonSchema).RootElement, schemaName: "schema")` (NO CreateJsonSchemaFormat); `AsIChatClient()` for OpenAI client.
- Polly 8.7.0: no-op pipeline = `ResiliencePipeline.Empty` (NOT .None); callback form `ExecuteAsync(async token => ..., ct)`.
- System.CommandLine 3.0.0-rc.1: no 4-binding SetHandler -> `command.SetAction(Func<ParseResult,CancellationToken,Task<int>>)` + `parseResult.GetValue(opt)` + `root.Parse(args).InvokeAsync()`.
- LibGit2Sharp 0.32: test init uses `repo.Commit("init", sig, sig)` (no overload-drift issue in prod path).
- Roslyn 5.9.0: `RegisterWorkspaceFailedHandler` if needed (WorkspaceFailed event obsolete CS0618); `SyntaxTrees.Count()` not SyntaxTreeCount().

## Known minors (deferred, logged in SDD ledger)
PathsMatch cross-boundary false-positive; ~~ArtifactWriter same-second overwrite + CurrentCulture dir stamp~~ (fixed pre-merge wave); ResolvedSymbols includes System.Object (benign); null-compilation path leaks workspace dispose; branch-name baseline untested; Cli.Tests trailing newline + template csproj property redeclaration.

## Anthropic-Messages adapter + LIVE verification (2026-09-09, branch feat/anthropic-adapter)
WHY: the only LLM access on this machine = Aliyun Token Plan gateway, **Anthropic-Messages protocol ONLY** (no OpenAI-compatible path; Content-Machine spike proved the token-plan key is rejected on the DashScope OpenAI endpoint). The MVP gateway spoke OpenAI-compatible → added an Anthropic adapter (gateway already `IChatClient`-abstract, so an added adapter not a rewrite). Design: `docs/superpowers/specs/2026-09-09-anthropic-adapter-design.md`.
- Spike (curl): `POST {base}/v1/messages`, `Authorization: Bearer` + `anthropic-version: 2023-06-01` → 200. Response `content[]` carries a `thinking` block + a `text` block; the client extracts ONLY `text`. qwen3.8-max returns clean JSON (reasoning stays in `thinking`).
- `AnthropicMessagesChatClient : IChatClient` (HttpClient, injectable handler for offline tests); `RouteOptions.Protocol` (openai|anthropic) + factory branch; `StructuredChat` ALWAYS injects the JSON-schema instruction into the prompt (Anthropic has no `response_format`) + client-side validate; appsettings route Protocol=anthropic, env-by-name.

LIVE VERIFIED — 3 runs reviewing the engine's own Llm project (diff vs master), creds sourced at runtime from Content-Machine/.env (never echoed/committed):
- **Zero-hallucination HELD on live LLM output:** ~50 cited symbols across runs, ALL `Verified:true`, 0 false. Models cited only real compiler-resolved symbols; nothing ungrounded reached the review.
- **Real findings (dogfooded):** missing `ConfigureAwait(false)` across the adapter, boxing at `AnthropicMessagesChatClient.cs:88`. SecurityAuditor correctly returned 0 findings (no false positives).
- **Resilience:** degraded-banner + quorum worked (one run 2/3 — qwen flash returns malformed JSON ~1/3 per Content-Machine spike; quorum 2 held, review completed).
- **LATENCY tuning:** 570s (qwen3.8-max, Polly retry2 × 2 routes × 90s) → 188s (all-flash, retry1, single route) → **130s** (all-flash, no-retry, timeout 120s). Still >60s target: Token Plan relay + Qwen reasoning ≈ 100-120s/persona on a real multi-file payload (Content-Machine logged similar).
- Config now: `ModelRoles` deep=fast=`qwen3.8-flash`; single Token Plan route; Polly = no-retry + 120s timeout + circuit breaker. Full suite 53/53 green offline.

PHASE-2 tuning queue (from live): payload trim (cap ResolvedSymbols + findings per method/file) to target <60s; single JSON-focused retry on `ModelException` for consistent 3/3 personas; per-route circuit breakers; OCE propagation.

## Phase 2 — Inc1: Reliability + Latency (2026-09-09, branch feat/phase2-inc1)
Shipped: `PayloadTrimmer` (caps symbols/allocations/methods/files in the LLM prompt view ONLY — guard keeps the FULL allow-list, zero-hallucination intact); PromptBuilder findings-cap; `StructuredChat` JSON-retry on `ModelException`; OCE propagation (gateway/loader/personarunner); persona-failure stderr diagnostics surfacing the `ProviderException` attempt-log; `max_tokens` 4096→8192; Polly no-retry + timeout 300s; **disabled `HttpClient` default 100s timeout** (`Timeout.InfiniteTimeSpan`).
ROOT CAUSE of live unreliability = the hidden `HttpClient` 100s default cap, which silently killed every qwen deep-reasoning call at 100s **regardless of the Polly timeout** (so all earlier Polly tuning was moot). NOT relay flakiness. With it disabled + Polly 300s, deep calls complete.
LIVE result (review Llm project, `--diff main`, 3 personas): **3/3 personas succeeded** (no degraded banner), 15 findings (5/persona), **audit 48 citations verified true / 0 false** (zero-hallucination held), WALL **282s**.
LATENCY reality: ~282s (4.7 min). qwen reasoning over a real review payload ≈ 100-280s/persona on the Token Plan relay; 3 parallel → wall = slowest. The <60s spec target is NOT reachable on this relay (reasoning-bound, not payload-bound; trim helped only marginally). Remaining levers (Phase 2+): a faster/non-reasoning model (none on Token Plan), fewer personas, streaming, or a different endpoint.
RELIABILITY: SOLVED (3/3 consistent once the HttpClient cap was removed).

## Commands
`dotnet build` · `dotnet test` (offline) · `TURTLE_LIVE=1 dotnet test` · `dotnet run --project src/CodeTurtleEngine.Cli -- review <repo> [--diff <ref>] [--project <path>] [--out <file>]`
(Live: source Bailian/Token-Plan creds into `TURTLE_LLM_BASE_URL` + `TURTLE_LLM_API_KEY` first. NOTE: after master→main rename, use `--diff main`.)
