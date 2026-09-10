# Architecture Decision Records

Decisions with recorded rationale live in the design specs and spike results. The candidates below are visible in the implementation but their formal rationale is not fully documented — they need owner confirmation before being promoted to accepted ADRs.

## Documented decisions

Rationale recorded at the time; treat these as the project's existing decision record.

### MSBuildWorkspace over Buildalyzer (Roslyn compilation loader)

- **Status:** Rationale recorded — [spikes/RESULTS.md](../../spikes/RESULTS.md) (Spike A, 2026-09-08) and the [MVP design spec](../superpowers/specs/2026-09-08-code-turtle-engine-design.md)
- **Decision:** Load compilations with `MSBuildWorkspace` (Roslyn 5.9.0) directly; no Buildalyzer dependency, no separate build roundtrip.
- **Evidence:** spike measured ~3s warm load, 0 compilation errors on a self-probe; no measured benefit from Buildalyzer.

### Anthropic-Messages adapter (Bearer auth, text-only extraction)

- **Status:** Rationale recorded — [adapter design spec](../superpowers/specs/2026-09-09-anthropic-adapter-design.md)
- **Decision:** Add a raw `HttpClient` adapter speaking the Anthropic Messages protocol (`Authorization: Bearer`, `anthropic-version: 2023-06-01`, `thinking` blocks ignored, `text` blocks extracted) as an additional `IChatClient` implementation behind the existing gateway abstraction.
- **Evidence:** the available relay (Aliyun Token Plan) exposes the Anthropic Messages protocol only; a curl spike confirmed the protocol before implementation.

## Candidate ADRs requiring owner input

Observed in code; rationale inferred or partially evidenced. **None of these are accepted** — the owner should confirm intent before formalizing.

### (a) No Polly retry policy — route-fallback-only resilience

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** `LlmGateway.DefaultPipeline` composes timeout (300s) + circuit breaker only; no `AddRetry`. Failed calls fall through to the next route in `LlmOptions.Routes` ([src/CodeTurtleEngine.Llm/LlmGateway.cs](../../src/CodeTurtleEngine.Llm/LlmGateway.cs)).
- **Evidence:** commit history shows deliberate retry tuning (retries were removed: `289d0a5`, `f778b06`) but no formal rationale document.
- **Open question:** is "no retry, fail fast to next route" the intended steady-state policy, or a latency experiment that should be revisited when a faster endpoint exists?

### (b) Gateway-global circuit breaker (not per-route)

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** one `ResiliencePipeline` is built per gateway instance and shared across all routes — a breaker trip affects every route, not just the failing one ([LlmGateway.cs](../../src/CodeTurtleEngine.Llm/LlmGateway.cs)).
- **Evidence:** code structure only; per-route breakers appear on the Phase 2 roadmap.
- **Open question:** intentional MVP simplification, or an oversight to fix before multi-route configs ship?

### (c) Deterministic Arbiter — no LLM conflict resolution

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** dedupe by (lowercased title, exact location), keep max severity, order by severity then location — pure code, no model call ([src/CodeTurtleEngine.Council/Arbiter.cs](../../src/CodeTurtleEngine.Council/Arbiter.cs)).
- **Evidence:** MVP design spec defers LLM-based arbitration to Phase 2.
- **Open question:** is determinism in the merge path a permanent invariant (auditability) or strictly a Phase 1 budget decision?

### (d) `Strip` as the default guard mode

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** `CouncilOptions.Guard` defaults to `Strip`; `Flag` mode records the audit in `audit.json` but leaves findings untouched in `review.md` ([src/CodeTurtleEngine.Council/CouncilOptions.cs](../../src/CodeTurtleEngine.Council/CouncilOptions.cs), [TurtleShellGuard.cs](../../src/CodeTurtleEngine.Council/TurtleShellGuard.cs)).
- **Evidence:** design spec §7 describes both modes; visible `[verified]`/`[unverified]` markers for Flag mode are deferred to Phase 2.
- **Open question:** should Strip remain default once Flag-mode markers exist, or is Flag the better default for transparency?

### (e) Prompt-injected JSON schema always; `response_format` as opportunistic bonus

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** `StructuredChat` always appends the schema + "respond with ONLY JSON" instruction to the prompt; for OpenAI-protocol routes it additionally tries `response_format`, degrading to the prompt-only call on failure ([src/CodeTurtleEngine.Llm/StructuredChat.cs](../../src/CodeTurtleEngine.Llm/StructuredChat.cs)).
- **Evidence:** adapter design spec notes the Anthropic Messages protocol has no `response_format`; client-side validation is mandatory on both paths.
- **Open question:** keep the dual path, or simplify to prompt-injection + validation only, given the Anthropic route is the one in production use?

### (f) Findings cap enforced in prompt text only

- **Status:** Proposed — awaiting owner confirmation
- **Observed implementation:** `MaxFindingsPerPersona` (default 5) is stated in the grounding rules given to the model; there is no post-hoc truncation of returned findings ([PromptBuilder.cs](../../src/CodeTurtleEngine.Council/PromptBuilder.cs), [PersonaRunner.cs](../../src/CodeTurtleEngine.Council/PersonaRunner.cs)).
- **Evidence:** code structure only.
- **Open question:** should the cap be enforced deterministically (take top-N by severity after parsing), or is prompt-level guidance acceptable because over-returning is rare and harmless?
