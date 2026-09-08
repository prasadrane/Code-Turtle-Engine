# NOTES — Code-Turtle-Engine build log (compressed)

## Status: MVP COMPLETE (2026-09-08) — Tasks 1, 2, 4–17 done; Task 3 creds-deferred
Branch `feat/mvp-implementation`. Offline suite green; full build 0W/0E. E2E proves core value path: real Roslyn compilation grounds council, Turtle Shell guard strips hallucinated citation.

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
- T16 (3938d49): CLI `review` (System.CommandLine 3.0.0-rc.1), ReviewPipeline diff->compile->payload->council->guard->markdown, ArtifactWriter 4 artifacts to $TURTLE_HOME/turtle/runs/, Composition DI, appsettings env-NAMES-only.
- T17 (this commit): e2e integration test (offline, CitingChat mock cites real payload symbol + fake; guard strips fake), LiveSmokeTests (self-skip unless TURTLE_LIVE=1), docs (README/AGENTS/rubric_v1), deleted 5 template UnitTest1.cs placeholders.

## Deferred / gated
- T3 Spike B (Bailian structured-output probe) + live smoke run: creds `TURTLE_LLM_BASE_URL`/`TURTLE_LLM_API_KEY` set on machine but not visible to tool shell. Unblock: ~/.bashrc exports or setx + restart. StructuredChat already ships degrade path.
- Phase 2 (per rulings, documented in README "MVP detection scope"): custom Roslyn analyzers for dangerous-cast / unawaited-task / captive-DI detection; LLM-based Arbiter conflict resolution; cloud/CI/GitHub/MCP; per-route circuit breakers; OCE propagation in PersonaRunner/guard pipelines.

## Key API adaptations (compiler-driven, verified)
- MEAI 10.9.0: schema = `ChatResponseFormat.ForJsonSchema(JsonDocument.Parse(jsonSchema).RootElement, schemaName: "schema")` (NO CreateJsonSchemaFormat); `AsIChatClient()` for OpenAI client.
- Polly 8.7.0: no-op pipeline = `ResiliencePipeline.Empty` (NOT .None); callback form `ExecuteAsync(async token => ..., ct)`.
- System.CommandLine 3.0.0-rc.1: no 4-binding SetHandler -> `command.SetAction(Func<ParseResult,CancellationToken,Task<int>>)` + `parseResult.GetValue(opt)` + `root.Parse(args).InvokeAsync()`.
- LibGit2Sharp 0.32: test init uses `repo.Commit("init", sig, sig)` (no overload-drift issue in prod path).
- Roslyn 5.9.0: `RegisterWorkspaceFailedHandler` if needed (WorkspaceFailed event obsolete CS0618); `SyntaxTrees.Count()` not SyntaxTreeCount().

## Known minors (deferred, logged in SDD ledger)
PathsMatch cross-boundary false-positive; ArtifactWriter same-second overwrite + CurrentCulture dir stamp; ResolvedSymbols includes System.Object (benign); null-compilation path leaks workspace dispose; branch-name baseline untested; Cli.Tests trailing newline + template csproj property redeclaration.

## Commands
`dotnet build` · `dotnet test` (offline) · `TURTLE_LIVE=1 dotnet test` · `dotnet run --project src/CodeTurtleEngine.Cli -- review <repo> [--diff <ref>] [--project <path>] [--out <file>]`
