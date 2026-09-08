# NOTES — Code-Turtle-Engine build log (compressed checkpoint)

## Phase 1 complete (2026-09-08) — scaffold + Spike A
Done:
- T1 scaffold: `CodeTurtleEngine.sln` (classic .sln), `Directory.Build.props` (net10.0/latest/nullable/implicit-usings/TreatWarningsAsErrors/RootNamespace=CodeTurtleEngine), `global.json` (SDK 10.0.100, rollForward latestFeature). 5 src (Core, Llm, Gatekeeper, Council, Cli) + 6 test projects. Refs: Cli→{Council,Gatekeeper,Llm,Core}; Council→{Core,Gatekeeper,Llm}; Llm→Core; Gatekeeper→Core; each test→subject; Integration.Tests→Cli. Build 0W/0E, tests green. commit 83bb258.
- T2 Spike A: MSBuildWorkspace works on net10 (Roslyn 5.9.0). DECISION: Task 10 uses MSBuildWorkspace (not Buildalyzer). ~3s warm load. commit 24c6f48. Evidence: `spikes/RESULTS.md`.

Deferred (creds-gated):
- T3 Spike B (Bailian structured output) + T17 LiveSmoke: need env `TURTLE_LLM_BASE_URL` / `TURTLE_LLM_API_KEY` (not provisioned). Task 8 StructuredChat ships BOTH schema-primary + degrade-fallback paths → not blocked by Spike B.

## Artifact state
- Branch `feat/mvp-implementation`. HEAD 24c6f48. Tree clean. `.superpowers/` (SDD ledger/briefs/reports) git-ignored.
- Toolchain: .NET SDK 10.0.400. git identity set local.
- SDD ledger: `.superpowers/sdd/2026-09-08-code-turtle-engine/progress.md`.

## Next — Phase 2 (on human confirm)
- T4 fixture SampleRepo (seeded defects) → T5 Core records → T6 ErrorClassifier → T7 ChatClientFactory+MockChatClient → T8 LlmGateway+StructuredChat. All OFFLINE (mock LLM; no creds).
- Carry to T10: Roslyn 5.9.0; use `RegisterWorkspaceFailedHandler` if needed; `SyntaxTrees.Count()`.

## LLM env (set on machine; do NOT paste secrets in chat)
- `TURTLE_LLM_BASE_URL` = Bailian OpenAI-compatible endpoint
- `TURTLE_LLM_API_KEY`  = key
- `TURTLE_HOME` (optional) = artifact root (default `~/.turtle`)
- `TURTLE_LIVE=1` enables live smoke test
