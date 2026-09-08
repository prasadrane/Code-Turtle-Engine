# Code-Turtle-Engine

AI pull-request reviewer that grounds LLM reasoning in deterministic Roslyn compiler
truths. A full-repo compilation feeds a minified semantic payload to a council of AI
personas; a Turtle Shell guard grounds findings in compiler truth: every symbol a
finding *cites* is verified against the Roslyn compilation, so cited APIs cannot be
hallucinated. Findings that cite no symbol pass through and are not fully grounded.

## MVP scope
Local CLI only: `diff -> compile -> payload -> council -> guard -> markdown`.
Cloud, CI, GitHub, and MCP integrations are on the roadmap (see the design spec).

## MVP detection scope
The gatekeeper deterministically extracts, per changed method: missing
`ConfigureAwait(false)` (async health), implicit boxing, LINQ closures, constructor
dependencies, and Roslyn-resolved symbols (the guard's citation allow-list).

**Deferred to Phase 2** (natural home: custom Roslyn analyzers):
dangerous-type-cast detection, unawaited-task detection, captive-DI-lifetime
detection.

The Arbiter is deterministic in the MVP (dedupe by title+location keeping the highest
severity, then severity ordering). LLM-based conflict resolution is Phase 2.

## Prerequisites
- .NET 10 SDK
- A C# repository to review (must compile)
- Alibaba Bailian (Token Plan) OpenAI-compatible endpoint credentials

## Setup
```bash
export TURTLE_LLM_BASE_URL="https://<bailian-compatible-endpoint>/v1"
export TURTLE_LLM_API_KEY="<key>"
# optional: export TURTLE_HOME="$HOME/.turtle"
```

## Build & test
```bash
dotnet build
dotnet test                       # offline suite
TURTLE_LIVE=1 dotnet test          # includes live Bailian smoke
```

## Run
```bash
dotnet run --project src/CodeTurtleEngine.Cli -- review /path/to/repo \
  --diff HEAD~1 --out review.md
```

MVP compiles a single project — pass `--project` to a `.csproj`, or for a `.sln` the FIRST project is compiled. Multi-project solution support is Phase 2.

## Docs
- Design spec: `docs/superpowers/specs/2026-09-08-code-turtle-engine-design.md`
- Implementation plan: `docs/superpowers/plans/2026-09-08-code-turtle-engine.md`
- Agent-ops contract: `AGENTS.md`
