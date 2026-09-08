# AGENTS.md — Code-Turtle-Engine

## Architecture map
- `CodeTurtleEngine.Core` — domain records (payload, findings, verdicts). No deps.
- `CodeTurtleEngine.Llm` — gateway: Bailian/Qwen via Microsoft.Extensions.AI, fallback, Polly, structured output, MockChatClient.
- `CodeTurtleEngine.Gatekeeper` — Roslyn: DiffProvider (LibGit2Sharp), MsBuildCompilationLoader, TurtleSyntaxWalker, SemanticAnalyzer, PayloadGenerator.
- `CodeTurtleEngine.Council` — RubricLoader, PromptBuilder, PersonaRunner (parallel), Arbiter (deterministic merge+markdown), TurtleShellGuard (zero-hallucination).
- `CodeTurtleEngine.Cli` — System.CommandLine `review` command, ReviewPipeline orchestration, ArtifactWriter, Composition (DI), TurtleOptions.

Dependency flow: Cli -> {Council, Gatekeeper, Llm} -> Core; Council -> Gatekeeper.

## Commands
- Build: `dotnet build`
- Test (offline): `dotnet test`
- Test (live Bailian): `TURTLE_LIVE=1 dotnet test`
- Run: `dotnet run --project src/CodeTurtleEngine.Cli -- review <repo> [--diff <ref>] [--project <path>] [--out <file>]`

## Invariants
- ≤300 lines per source file.
- Payload-only to the LLM; never send raw source.
- Fail closed: no compilation ⇒ no review.
- Secrets only in env (`TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`); config references env by name.
- TDD: failing test first; commit each task.

## Env
- `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`, `TURTLE_HOME` (default `~/.turtle`), `TURTLE_LIVE=1` (live smoke).

## Agent protocol
1. Query codebase-memory MCP / graph tools before reading files broadly.
2. Check `NOTES.md` for current build state.
3. Tests-first; run the offline suite; keep it green before committing.
