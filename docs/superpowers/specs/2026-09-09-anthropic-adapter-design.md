# Code-Turtle-Engine — Anthropic-Messages Adapter Design

**Date:** 2026-09-09
**Status:** Approved (build) — post-MVP feature enabling live runs on the Aliyun Token Plan gateway.

## Why
The MVP Llm gateway speaks **OpenAI-compatible** (`BailianChatClientFactory` → OpenAI SDK `/chat/completions`). The only LLM access on this machine is the **Aliyun Token Plan gateway**, which exposes **only the Anthropic Messages protocol** — there is no OpenAI-compatible path (Content-Machine `spikes/RESULTS.md` 2026-09-04 probed `/compatible-mode/v1`, `/apps/openai/v1`, `/v1` → 401/404; the token-plan key is rejected on the standard DashScope OpenAI endpoint). To run the engine live, add an **Anthropic-Messages `IChatClient` adapter**. The gateway is already provider-agnostic (`IChatClient`), so this is an added adapter, not a rewrite.

## Relay protocol — CONFIRMED via curl spike (2026-09-09)
- **Endpoint:** `POST {BASE_URL}/v1/messages` where `BASE_URL = https://token-plan.ap-southeast-1.maas.aliyuncs.com/apps/anthropic`.
- **Headers:** `Authorization: Bearer {TOKEN}`; `anthropic-version: 2023-06-01`; `content-type: application/json`.
- **Request body:** `{"model":"qwen3.8-flash|max","max_tokens":N,"system":"<optional>","messages":[{"role":"user|assistant","content":"<string>"}]}`.
- **Response 200:** `{"id","type":"message","role":"assistant","model","content":[{"type":"thinking",...},{"type":"text","text":"<answer>"}],"stop_reason","usage":{...}}`.
- **CRITICAL:** `content[]` may contain a `thinking` block (Qwen reasoning) **before** the `text` block. The adapter must extract and concatenate **only** `type=="text"` blocks and ignore `thinking`.
- **Latency:** qwen3.8-flash ~2.8s; qwen3.8-max ~29–50s (reasoning). Personas run in parallel → wall-clock up to ~50s.
- **Models confirmed:** `qwen3.8-max` (deep), `qwen3.8-flash` (fast).

## Design
1. `RouteOptions` gains `Protocol` (`"openai"` | `"anthropic"`), default `"openai"` (existing routes unaffected).
2. New `AnthropicMessagesChatClient : IChatClient` (in `CodeTurtleEngine.Llm`): `HttpClient` POST to `{baseUrl}/v1/messages` with Bearer + `anthropic-version`; map `ChatMessage[]` (System role → top-level `system` string concatenated; User/Assistant → `messages`); `max_tokens` from options (default 4096); parse `content[]` text blocks → `ChatResponse`. Non-streaming (`GetStreamingResponseAsync` minimal/empty). Non-200 → throw carrying the HTTP status so `ErrorClassifier`/gateway fallback still work.
3. `IChatClientFactory.Create(route, model)` becomes protocol-aware: `openai` → existing OpenAI SDK path; `anthropic` → `AnthropicMessagesChatClient`. Env resolved by name (`BaseUrlEnv`/`ApiKeyEnv`) exactly as today.
4. `StructuredChat`: Anthropic Messages has **no** `response_format json_schema`. Make `StructuredChat` **always inject the JSON-schema instruction into the prompt** (reliable for both protocols) and rely on client-side `Parse`+validate (`ExtractJson` + `JsonSerializer` + `ModelException`). The OpenAI `ForJsonSchema` path may remain as a bonus when `Protocol=openai`, but the prompt instruction is the dependable path. The relay has **no grammar guarantee** (Content-Machine spike: ~1/3 malformed JSON) → client-side validation is mandatory (already present).
5. **Polly timeout 30s → 90s** (relay qwen3.8-max 29–50s). Keep retries low (1) so worst-case latency stays bounded; note the ~50s wall-clock against the 60s spec target.
6. `appsettings.json`: add/point a Token Plan route — `Protocol=anthropic`, `BaseUrlEnv=TURTLE_LLM_BASE_URL` (= `.../apps/anthropic`), `ApiKeyEnv=TURTLE_LLM_API_KEY` (= token), models `qwen3.8-max`/`qwen3.8-flash`. **Env by NAME only; the key is never committed.**

## Live verification (creds sourced at runtime from `Content-Machine/.env`; never echoed, never committed)
- Relay structured probe (Spike-B-equivalent): does Qwen return schema-valid `VerdictDto` JSON via prompt? Measure malformed rate.
- `TURTLE_LIVE=1` live smoke.
- Real `turtle review <csharp-repo> --diff <ref>` → verify personas produce grounded findings, the guard strips hallucinations, `review.md` + artifacts are written, wall-clock is acceptable.

## Out of scope (Phase 2)
- Anthropic streaming (SSE).
- Anthropic native tool-use / `parse` structured output (rely on prompt + validate).
- Multi-protocol cross-provider fallback chains (single Token Plan route for now).
