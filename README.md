# 🐢 Code Turtle Engine

Compiler-grounded AI code review for C#, with a local CLI and self-hosted beta web UI.
Roslyn extracts facts from changed members, three model personas suggest findings,
and a deterministic guard checks cited type/member names. This can reduce invented
symbol references; it does not verify recommendation correctness or changed-code relevance.

[![CI](https://github.com/prasadrane/Code-Turtle-Engine/actions/workflows/ci.yml/badge.svg)](https://github.com/prasadrane/Code-Turtle-Engine/actions/workflows/ci.yml)

![Web dashboard preview (historical)](docs/assets/dashboard-preview.png)

The web API now defaults to disabled; the browser demo remains offline. See
[isolation and local-access controls](docs/ISOLATION.md) before enabling a trusted-local
server. Public source acquisition and hosted release remain gated.

## Verification policy

CLI, web and the guarded benchmark arm use the full untrimmed payload and a
compilation oracle. Citation audits distinguish `Payload`, `Compilation`, and
`Rejected` evidence. Existence is at type/member-name granularity: parameter lists
are normalized away, so exact overloads, calls, locations, relevance and prose are
not verified. Compilation-only existence may refer to unrelated code.

Strip removes rejected citations and findings with no remaining cited symbols.
Uncited findings remain advisory by default; `RequireCitationsInStripMode=true`
drops them. Flag preserves originals, including rejected and uncited findings,
and records the audit. See the [behavior matrix](docs/VERIFICATION_CONTRACT.md).

Workspace failures and project compilation errors stop review. Per-project Roslyn
compilations preserve binding contexts. Review never restores dependencies
automatically. Restore/build trusted target projects yourself first.

Product prompts contain semantic metadata rather than raw source bodies. Metadata
can disclose proprietary symbols, paths, dependencies and risk descriptions.
This is not a privacy or security guarantee. The beta evaluates MSBuild on the host;
use trusted repositories until the later isolation gate is implemented.

## Extracted facts

TurtleSyntaxWalker visits methods, constructors and executable properties.
`MethodFacts` contains `Method`, `Allocations`, `AsyncHealth`, `Dependencies`,
`ResolvedSymbols`, 1-based `StartLine`/`EndLine`, and optional `Risks`.
Risk signals are heuristic human-readable descriptions, not boolean defect proofs.

| Source | Actual output examples |
|---|---|
| AsyncHealth | `Missing ConfigureAwait(false) on … invocation (Line …)` |
| Allocations | `Implicit Boxing (Line …)` (object or interface conversion), `LINQ Closure (Line …)` |
| Risks: async | `Unawaited Task (fire-and-forget) on …`, `Sync-over-async blocking call …` |
| Risks: input/security | `Dynamic SQL string concatenation/interpolation …`, `Hardcoded secret/credential …`, `Unvalidated parameter … passed to Path.Combine …` |
| Risks: architecture | `Service locator call … on IServiceProvider`, `Captive dependency: Singleton … depends on Scoped/Transient …` |
| Risks: reliability | `Swallowed exception in catch block without logging or rethrow …`, `Unsynchronized access to static non-thread-safe collection …` |

DI lifetime detection compares registrations inside the same member, not across the
solution. Weak crypto, weak random, lock-in-async and LOH are not extractor tags.
Frontend risk badges are inferred from finding text; they are not direct compiler
proofs. The UI supports severity/persona/keyword filtering, a raw persona audit tab,
markdown output, and playable historical demos. Streamed findings are provisional.

## Build and test offline

Requires .NET SDK 10.0.100 (`global.json`) and Node 24.19.0 for the web UI.
In the published cloud environment, source `/workspace/.cloud-setup/activate.sh`
before .NET commands in each shell.

```bash
export TURTLE_LIVE=0
dotnet build -c Release -m:2
dotnet test -c Release --no-build -m:2
cd web
npm ci
npm test
npm run typecheck
npm run build
```

Offline tests require no LLM credentials. The opt-in live smoke returns early unless
TURTLE_LIVE=1; its offline result is not a live model test.

## CLI and self-hosted beta

Configure `TURTLE_LLM_BASE_URL` and `TURTLE_LLM_API_KEY` for a supported provider
before a model review. The gateway supports Anthropic Messages and OpenAI-compatible
routes. Structural payloads are sent to the configured provider.

```bash
dotnet run --project src/CodeTurtleEngine.Cli -- review /path/to/trusted/repo --diff main --out review.md
# Separate terminals for the beta API and dashboard:
dotnet run --project src/CodeTurtleEngine.Web --urls http://localhost:5000
cd web && npm run dev
```

Open localhost:3000; local demos need no provider calls. Public-hosting isolation
and security work remain out of scope; no deployment configuration is claimed.

`review <repo>` supports `--project <csproj|sln>`, `--diff <ref>`, and `--out <file>`.
With `--diff`, baseline-to-HEAD excludes uncommitted changes; without it, HEAD is
compared with the working directory. Solutions retain separate C# project compilations.
Exit codes are 0 for success and 1 for pipeline error. A degraded quorum review is
explicitly labeled, not clean or complete council participation.

Artifacts under `$TURTLE_HOME/reviews` (default `~/.turtle/reviews`) include full
payload.json, raw verdicts.json, final review.md, citation audit.json, and
finding-audit.json and compilation-diagnostics.json. Council configuration includes Quorum, Strip/Flag,
RequireCitationsInStripMode, prompt caps and MaxFindingsPerPersona. JsonRetries
bounds retries for malformed or schema-invalid model output.

## Historical benchmark evidence

The saved [benchmark checkpoint](docs/benchmark/claude-sonnet-resume.md) predates the
member/risk expansion. Its three-repetition run used mixed models and reports:

| Arm | Precision | Recall | Invented citations |
|---|---|---|---|
| Naive | 14.8% (30/203) | 83.3% (30/36) | 4.3% (23/531) |
| Grounded | 21.2% (22/104) | 61.1% (22/36) | 0% (0/217) |
| Grounded + guard | 21.2% (22/104) | 61.1% (22/36) | 0% (0/217) |

Grounded and guarded results were identical; this evidence shows no incremental
guard benefit. It does not establish faster merges, ROI, competitive superiority,
or current model performance. Deterministic seeded fixture coverage is separate
from empirical model quality. No new model responses were collected for hardening.
`turtle bench` supports explicit recording/replay; preserve original model and
harness provenance when evaluating saved answers.

## Structure and documentation

`src/` contains Core, Gatekeeper, Llm, Council, Cli and Web projects. `web/` is the
Next.js/React dashboard. `tests/` contains offline backend fixtures; `web/tests/`
contains UI regressions. `bench/` holds the corpus and `turtle/` the review rubric.

- [Architecture](ARCHITECTURE.md)
- [Verification contract](docs/VERIFICATION_CONTRACT.md)
- [Hardening validation](docs/REVIEW_HARDENING_VALIDATION.md)
- [Contributing](CONTRIBUTING.md), [Security](SECURITY.md), [MIT license](LICENSE)
