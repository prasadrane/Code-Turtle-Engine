# Phase 2 — Increment 1: Reliability + Latency

> **Historical implementation plan.** References NOTES.md, an internal build log no longer tracked in this repository; evidence lives in commit messages (bb402e0 and predecessors).

**Goal:** Make the live-verified reviewer practical — cut wall-clock latency toward the <60s target and make council runs consistently return 3/3 personas — without weakening the zero-hallucination guarantee.

**Spec:** `docs/superpowers/specs/2026-09-08-code-turtle-engine-design.md` (MVP) + `docs/superpowers/specs/2026-09-09-anthropic-adapter-design.md` + `NOTES.md` (live findings: ~130s, occasional degraded 2/3 from ~1/3 malformed-JSON rate on qwen flash).

**Branch:** `feat/phase2-inc1`. Inherits all MVP global constraints (net10.0, `CodeTurtleEngine` root, ≤300 lines/file, TreatWarningsAsErrors, secrets env-by-name only, offline-first tests, TDD, conventional commits).

## Core design principle (do not violate)
**Trim the LLM prompt view, never the guard allow-list.** The Turtle Shell guard must keep verifying against the FULL `ResolvedSymbols` from the compilation. Latency/size reductions apply only to the payload projection sent to the personas. Since a model can only cite symbols it is shown, and every shown symbol is a subset of the full allow-list, trimming the prompt cannot cause a valid finding to be stripped, and cannot let an ungrounded one through. The zero-hallucination guarantee is preserved by construction.

---

### Task 1: Payload trimmer + findings cap (latency)

Reduce model input + output tokens (the dominant latency driver is Qwen reasoning over the prompt).

**Files:**
- Create: `src/CodeTurtleEngine.Council/PayloadTrimmer.cs`
- Modify: `src/CodeTurtleEngine.Council/CouncilOptions.cs` (add trim/cap settings), `src/CodeTurtleEngine.Council/PromptBuilder.cs` (findings-cap instruction), `src/CodeTurtleEngine.Cli/ReviewPipeline.cs` (wire trimmed payload to council, full payload to guard), `src/CodeTurtleEngine.Cli/appsettings.json` (defaults)
- Test: `tests/CodeTurtleEngine.Council.Tests/PayloadTrimmerTests.cs`, extend `PromptBuilderTests.cs`

**Interfaces:**
- `static class PayloadTrimmer { RoslynPayload TrimForPrompt(RoslynPayload full, CouncilOptions opts) }` — returns a NEW payload with, per method: `ResolvedSymbols` capped to first `opts.MaxSymbolsPerMethod`, `Allocations` capped to first `opts.MaxAllocationsPerMethod`; per file: `Methods` capped to first `opts.MaxMethodsPerFile`; `Files` capped to first `opts.MaxFiles`. `RepoSlug`/`DiffBaseline` preserved. `AsyncHealth`/`Dependencies` unchanged. Never mutates the input.
- `CouncilOptions` gains: `int MaxSymbolsPerMethod = 20`, `int MaxAllocationsPerMethod = 10`, `int MaxMethodsPerFile = 40`, `int MaxFiles = 40`, `int MaxFindingsPerPersona = 5`.
- `PromptBuilder.Build` appends to the grounding rules: "Return AT MOST {MaxFindingsPerPersona} findings, highest severity first." (pass the cap in, or read from a new `PromptBuilder.Build(role, payload, rubric, maxFindings)` overload).
- `ReviewPipeline.RunAsync`: build FULL payload; `var promptPayload = PayloadTrimmer.TrimForPrompt(payload, options.Council);` pass `promptPayload` to `_personas.RunAsync(...)`; pass the FULL `payload` to `_guard.Verify(merged, payload)`. (Pipeline needs `CouncilOptions` — it already has `TurtleOptions.Council`.)

**Tests (TDD):**
- PayloadTrimmer: given a payload with a method having >cap symbols/allocations and a file with >cap methods, assert the trimmed copy caps each list and does NOT mutate the original; assert RepoSlug/DiffBaseline preserved.
- PromptBuilder: assert the system message contains the "AT MOST {N} findings" instruction.
- ReviewPipelineTests: existing test must stay green (it uses FakePersonas; trimming an empty stub payload yields empty — fine). Optionally assert the guard still receives the full payload (the strip-assertion already covers grounding).

---

### Task 2: JSON-retry (reliability) + OperationCanceledException propagation

**Files:**
- Modify: `src/CodeTurtleEngine.Llm/StructuredChat.cs` (JSON retry), `src/CodeTurtleEngine.Llm/LlmGateway.cs` (OCE rethrow), `src/CodeTurtleEngine.Gatekeeper/CompilationLoader.cs` (OCE rethrow), `src/CodeTurtleEngine.Council/PersonaRunner.cs` (OCE rethrow), `src/CodeTurtleEngine.Llm/LlmOptions.cs` or `CouncilOptions` (retry setting)
- Test: extend `tests/CodeTurtleEngine.Llm.Tests/StructuredChatTests.cs`, add OCE cases

**Interfaces / changes:**
- **JSON retry:** `StructuredChatClient.CompleteStructuredAsync<T>` — when `Parse<T>` throws `ModelException` (malformed/invalid JSON), retry the gateway call up to `JsonRetries` times (default 1) before giving up. Each retry re-requests (re-rolls the model). Keep the existing schema→degrade behavior; the retry wraps the parse-validation. Add `int JsonRetries` to config (LlmOptions or a small StructuredChatOptions). Net effect: a persona whose first JSON is malformed re-rolls once → ~89% per-persona success vs ~67%, so 3/3 consistency rises sharply.
- **OCE propagation:** in `LlmGateway.CompleteAsync` per-route catch, `CompilationLoader.LoadAsync` catch-all, and `PersonaRunner.RunOneAsync` catch — add `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }` BEFORE the generic catch, so user cancellation surfaces as `OperationCanceledException`, not masked as `ProviderException`/`CompilationException`/null-quorum. (The loader's catch is `when (ex is not CompilationException)` — also exclude OCE: `when (ex is not CompilationException && ex is not OperationCanceledException)`.)

**Tests (TDD):**
- StructuredChat: a FakeGateway returning malformed JSON on call #1 then valid JSON on call #2 → `CompleteStructuredAsync` succeeds (proves retry). A gateway always-malformed → throws `ModelException` after `JsonRetries+1` attempts (assert attempt count).
- OCE: gateway/loader/persona — when the delegate throws `OperationCanceledException` with `ct` cancelled, assert it propagates as `OperationCanceledException` (not ProviderException/null).

---

### Task 3: Live verification + NOTES

**Files:** modify `NOTES.md` (record Increment-1 results). No production code.

**Steps (controller-run, creds sourced at runtime from `Content-Machine/.env`, never echoed/committed):**
1. `dotnet build` 0W/0E + `dotnet test` full suite green offline.
2. Live: `dotnet run --project src/CodeTurtleEngine.Cli -- review . --project src/CodeTurtleEngine.Llm/CodeTurtleEngine.Llm.csproj --diff main` (review the engine's own Llm project; `main` is the base now).
3. Measure + record: wall-clock (target < prior 130s; report actual), persona success (target 3/3, no degraded banner), guard audit (all Verified:true, 0 false — guarantee still holds), AND confirm the engine NO LONGER flags the adapter's `ConfigureAwait`/boxing (the dogfood fix from `fcc0638` resolved them).
4. Update `NOTES.md` with Increment-1 outcome (latency before/after, reliability, dogfood-fix confirmation). Commit.

**Acceptance:** live review completes; latency improved (report actual vs 130s); zero-hallucination still holds (0 unverified citations); dogfood findings gone. If latency still >60s, document the residual (relay/model reasoning floor) — payload trim + findings cap are the available levers; deeper cuts (smaller model, streaming, async persona cap) are Phase 2+.

---

## Out of scope (later Phase 2 increments)
Deterministic Roslyn analyzers (Inc 2), GitHub/MCP integration (Inc 3), SQLite persistence (Inc 4), cloud (Inc 5). Per-route circuit breakers, multi-project solution compile, Flag-mode markers — deferred (already in NOTES).
