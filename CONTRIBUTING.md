# Contributing

## Prerequisites

- .NET 10 SDK — `global.json` pins `10.0.100` (`rollForward: latestFeature`)
- No LLM credentials needed for the offline suite; live tests require `TURTLE_LLM_BASE_URL` + `TURTLE_LLM_API_KEY` (an Anthropic-Messages-protocol or OpenAI-compatible endpoint)

## Build & test

```bash
dotnet build          # warnings are errors (TreatWarningsAsErrors in Directory.Build.props)
dotnet test           # 64 offline tests — hermetic, no network, no credentials
TURTLE_LIVE=1 dotnet test   # adds live smoke tests against a real LLM endpoint
```

CI runs `dotnet build --configuration Release` + `dotnet test --configuration Release --no-build` on push to `main` and on pull requests ([.github/workflows/ci.yml](.github/workflows/ci.yml)).

## Conventions

From [AGENTS.md](AGENTS.md) — the same contract the project's agents work under:

- Source files ≤ 300 lines
- Fail closed: no compilation ⇒ no review
- Payload-only to the LLM; never send raw source
- Secrets only via environment variables — config references env-var *names*, never literal values
- TDD: failing test first; commit each task

## Commits

Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `perf:`, `ci:`). History follows this — see `git log`.

## Pull requests

1. Branch from `main`
2. Keep the offline suite green (`dotnet test`)
3. Do not weaken the zero-hallucination chain: the Turtle Shell guard must audit against the FULL payload, never a trimmed view
4. Link the issue the change addresses, if any
