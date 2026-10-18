# Review hardening validation (steps 1–7)

Historical phase report. Subsequent steps 8–11 preparation and remaining release
gates are recorded in [FOLLOWON_VALIDATION.md](FOLLOWON_VALIDATION.md).

Starting commit: `a98b7cf219f3e81cad1b356c23c668bd3ff357dd` (the reviewed baseline).
Branch: `codex/review-hardening`. No upstream changes were reset or overwritten.
Tools: .NET SDK 10.0.100; Node v24.19.0; npm 11.9.0; resolved Next.js 15.5.25.
All .NET shells source `/workspace/.cloud-setup/activate.sh`; TURTLE_LIVE=0 throughout.

## Acceptance and regression map

| Step | Implemented behavior and evidence |
|---|---|
| 1 | Baseline retained from an untouched `git archive a98b7cf` in ignored work/baseline (not a new worktree). Release build and 288 backend tests passed. Frontend baseline: 69 tests, TypeScript and production build passed. Final outcomes below are separate evidence. |
| 2 | VERIFICATION_CONTRACT.md defines valid/invalid/mixed/absent citations for Strip/Flag and advisory/strict. Citation evidence and finding retention/status are serialized separately. VerificationContractTests covers all four option combinations, uncited status and immutable originals. |
| 3 | Workspace failures and compilation errors stop review; warnings are surfaced. Per-project compilations preserve binding, references and options. CompilationQualityTests covers missing dependencies without restore, error symbols, duplicate names, conditional parse options and project references; CompilationLoaderTests exercises real multi-project loading. Trusted benchmark restore has a deadline, cancellation, bounded output and process-tree termination. No automatic product restore was enabled. |
| 4 | CLI, web and derived benchmark guard receive the compilation oracle and full payload. Payload/Compilation/Rejected evidence is explicit; namespace prefixes are not inferred. CLI OracleWiringTests, WebOracleWiringTests and BenchRunnerTests cover broader real names, fabricated members, mixed/uncited/Flag behavior and original benchmark findings. Secondary-project benchmark label enrichment and source lookup use aggregate trees without flattening binding. |
| 5 | StructuredChat validates the actual review schema vocabulary before deserialization, including required/null/type/enum/items/additional-property rules and duplicate JSON keys. Unsupported schema keywords fail as configuration errors. SchemaValidationTests checks invalid JSON shapes and bounded retries; cancellation and unrelated transport failures cannot trigger format fallback. Existing Anthropic/OpenAI-compatible adapter tests remain in the offline suite. |
| 6 | SSE findings explicitly say Provisional. UI badges require provenance-bearing audit evidence, final stream state reconciles with synthesized findings, and raw persona output is labeled as an audit view. Legacy demos remain playable with unavailable provenance. Empty/degraded/all-removed reports avoid clean-code approval claims. verification-states tests cover fabricated/legacy/uncited states, rejection visibility, partial-citation reconciliation and failed review state. The compilation-failure regression also repaired premature SSE channel closure during clone cleanup. |
| 7 | README/ARCHITECTURE and active guidance match actual extractor descriptions, member walking, interface boxing, web project, per-project compilation and existence-only policy. Unsupported Compose instructions, absolute guarantees and competitive claims removed. Historical mixed-model metrics confirmed against docs/benchmark/claude-sonnet-resume.md; no incremental guard benefit claimed. Frontend CI pins Node 24.19.0 and runs npm ci/tests/typecheck/build alongside backend Release coverage. Clipboard test now awaits React act correctly. |

Codebase-memory skill/graph tools were unavailable. Source and relied-on tests were
inspected directly; the table maps behavior coverage, not a claimed graph or coverage
percentage. Changed source files obey the 300-line limit (existing unrelated source
files were not reformatted). Fail-closed compilation is intentionally stricter than
the previous non-null-compilation check: there is no partial-error review mode.

## Commands and outcomes

Baseline backend commands (from repository root, after activation):

```bash
mkdir -p work/baseline
git archive a98b7cf | tar -x -C work/baseline
TURTLE_LIVE=0 dotnet build work/baseline/CodeTurtleEngine.sln -c Release -m:2
TURTLE_LIVE=0 dotnet test work/baseline/CodeTurtleEngine.sln -c Release --no-build -m:2
```

Completed: build with 0 warnings/errors; 288 tests passed (including the live smoke's
intentional early return, not a live request). Baseline frontend, from web/: `npm test
-- --run`, `npx tsc --noEmit`, `npm run build`: 69 passed; types/build completed.

The initial restricted-sandbox backend attempt failed before compilation: MSBuild's
named-pipe socket bind raised SocketException (13), despite its summary saying zero
errors. It was not counted as a pass. Rerunning with local process/socket access
completed. The first restricted npm ci stalled and was terminated; the allowed-registry
rerun below completed. An added failure-path regression found and repaired premature SSE channel completion before error emission. Intermediate failing expectations were corrected to assert
per-project compilation, authoritative oracle rejection and final replay reconciliation.

Final commands (repository root, after activation for .NET):

```bash
export TURTLE_LIVE=0
dotnet build -c Release -m:2
dotnet test -c Release --no-build -m:2 --logger 'trx;LogFilePrefix=completed' --results-directory work/completed-results
npm --prefix web ci --cache /workspace/Code-Turtle-Engine/work/npm-cache
npm --prefix web test
npm --prefix web run typecheck
npm --prefix web run build
git diff --check
```

Completed final outcomes:

| Check | Outcome |
|---|---|
| Release backend build | Passed, 0 warnings, 0 errors |
| Offline backend tests | 320 passed, 0 failed: Core 3; CLI 79; Council 33; Gatekeeper 60; Llm 43; Web 98; Integration 4 |
| Clean npm ci | Passed, 210 packages installed; lockfile unchanged |
| Frontend Vitest | 74 passed across 8 files; no React act warning |
| TypeScript | Passed (tsc --noEmit) |
| Next.js production build | Passed; static routes generated |
| Diff/source-size checks | git diff --check passed; all changed source files at most 300 lines |

The final backend logs are work/completed-build.log and work/completed-tests.log;
TRX files are under work/completed-results. Frontend logs are
work/npm-ci-completed.log, work/final-web-tests.log, work/final-web-types.log,
and work/final-web-build.log. Baseline logs use the baseline- prefix.
Intermediate failures are not counted as acceptance passes.

Logs and TRX files are retained under ignored work/. The review bundle contains
baseline/final logs and a binary-safe diff; it excludes node_modules, credentials,
raw source dumps and benchmark answers. CI workflow execution on GitHub is separate
from the equivalent completed local commands.

## Preserved evidence, limits, and review handoff

The tracked docs/assets/dashboard-preview.png is unchanged. The two local-only
INTERVIEW_PREP and UNDERSTAND_ANYTHING documents are absent here and were not
reconstructed. Ignored benchmark checkpoints and saved model/harness provenance
were not modified. Historical design plans remain snapshots; current behavior is
defined by the verification contract and architecture reference.

No steps 8–11, live model calls, public-repository review jobs, paid provider calls,
new empirical benchmark responses, merge, or deployment were performed. Mock-backed
benchmark unit tests and deterministic fixture extraction are offline regression
evidence only. MSBuild still evaluates trusted projects on the host; this work does
not establish isolation or public-hosting security readiness. Dependency diagnostics
can stop reviews that previously appeared complete. Exact overload correctness and
recommendation relevance remain deliberately outside citation existence checks.

Draft PR creation is blocked by repository write access: `git push --dry-run origin
HEAD:refs/heads/codex/review-hardening` returned HTTP 403 (permission denied).
`gh auth status` also reported invalid GH_TOKEN authentication. No remote branch
was created. The scoped local branch, committed reviewable cloud diff, binary-safe
patch and prepared PR description provide the authorized fallback; no merge occurred.
