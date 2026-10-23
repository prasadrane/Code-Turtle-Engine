# Architecture — Code Turtle Engine

Code Turtle Engine provides a local CLI and a self-hosted beta web interface for
C# review. Roslyn facts constrain model input and deterministic citation existence
checks can reduce invented symbol references. Recommendations still require review.
The [verification contract](docs/VERIFICATION_CONTRACT.md) defines all labels and modes.

## Components and dependencies

- `Core`: payloads, findings, citation/finding audits, JSON serialization; no project dependencies.
- `Gatekeeper`: LibGit2Sharp diffs, MSBuildWorkspace loading, member walking,
  semantic facts, risk heuristics, and SymbolOracle; depends on Core.
- `Llm`: OpenAI-compatible and Anthropic Messages adapters, route fallback,
  Polly timeouts/circuit breaker, structured output; depends on Core.
- `Council`: prompt construction/trimming, persona quorum, deterministic arbiter,
  TurtleShellGuard; depends on Core, Gatekeeper, Llm.
- `Cli`: review orchestration, artifact writing, and benchmark tooling.
- `Web`: ASP.NET Core PR metadata, clone service, in-memory job store, SSE pipeline;
  reuses the council and gatekeeper. Next.js/React in `web/` consumes its events.

## Compilation and facts

MSBuildWorkspace opens a project (including its referenced projects) or solution.
Workspace failures, null compilations, no C# projects, and compilation errors stop
review before any persona request. Exceptions include bounded diagnostic details.
Warnings do not imply incompleteness. Restore is **not automatic** in CLI/web review.
MSBuild evaluation is not an isolation boundary; only trusted repositories belong
in this beta until the later isolation/security gate is implemented.

`LoadedCompilation.Projects` preserves each project's compilation. Its `Compilation`
property remains a compatibility view of the first project, not an aggregate.
PayloadGenerator analyzes each changed tree using its owning compilation's semantic
model; parse options, project references, preprocessor symbols and duplicate type
names are not flattened. Linked files may yield facts in multiple project contexts.
SymbolOracle aggregates lookup conservatively: duplicate fully qualified source types
without project identity are rejected as ambiguous. Error types and members with
unresolved signatures are excluded from evidence. Metadata references are searched
within their original project contexts.

TurtleSyntaxWalker visits methods, constructors, and properties with executable
bodies/accessors. SemanticAnalyzer records member line spans, constructor dependency
names, resolved symbols, allocation signals, async health, and risk descriptions.
Boxing includes value-type conversion to object **or interface**. Capturing LINQ
lambdas include external locals, parameters, and instance fields; this is a heuristic.
RiskDetector emits human-readable signals described in README, not a comprehensive
security analysis. DI lifetime checks search registrations within the **same member**;
they do not infer solution-wide service lifetimes.

## Review flow

Diff → per-project compilations → full payload → trimmed prompt projection → three
parallel personas → deterministic arbitration → citation guard → markdown/artifacts.

Prompt trimming does not modify the full payload used by verification. Product
configuration enforces Grounded prompts: source bodies are reserved for the explicit
programmatic Naive benchmark baseline. Metadata can disclose proprietary names,
paths, dependencies and risk descriptions; this is not a privacy guarantee.

StructuredChat validates required properties, nulls, types, enum values, array items,
and additional-property restrictions before deserialization. Its validator supports
the review schema vocabulary; unsupported schema keywords are configuration errors,
not silently ignored constraints. Duplicate JSON keys are rejected. Malformed or
schema-invalid output raises ModelException and receives bounded JsonRetries retries.
An explicitly unsupported response-format capability can fall back to prompt-only
output. Cancellation and unrelated provider/transport errors do not trigger a second
StructuredChat request. Gateway route fallback remains a separate transport policy.

The arbiter deduplicates normalized title + location, retaining the highest severity,
then sorts deterministically. Guard Strip/Flag policies and uncited advisory/strict
behavior are defined in the contract. Name-only checks do not select overloads. Supported explicit parameter lists are
conservatively matched against declaration signatures; assembly/declaration identities
and name-level extracted context are recorded separately. Unsupported signatures
reject, and no check establishes call-site binding. No citation label verifies
prose, relevance, locations, or recommended changes.

## Web states

Streaming persona findings are provisional. Completion reconciles working findings
with synthesized results and audit evidence. Raw persona output is labeled as an
audit view; rejected citations remain visible there. Citation chips require Payload
or Compilation evidence to show verification. Legacy demo booleans without provenance
are marked unavailable. Guard rejection differs from actual stripping because Flag
retains rejected citations. Empty and degraded reviews do not claim approval or clean
code. Compilation failures produce an error, not a completed empty review.

The server job store is in memory; cloning and MSBuild run on the host. This is not
public hosting isolation or deployment readiness. No validated Docker Compose setup
is shipped. Repository execution/isolation and hosting gates remain future work.

## Artifacts and evaluation

CLI writes full payload.json, raw verdicts.json, review.md, citation audit.json,
finding-audit.json and compilation-diagnostics.json under TURTLE_HOME/reviews. Web serializes the same verdict
contracts through SSE. Originals are preserved when stripped copies are derived.

BenchRunner requests Naive/Grounded pipelines in Flag mode and derives the guarded
arm from Grounded findings using the same compilation oracle. BenchScorer reports
payload support separately from invented names and defect-label precision/recall.
Trusted benchmark restore has a two-minute deadline, cancellation, process-tree
termination and bounded output tails. It is not wired into product review.

Historical benchmark evidence lives in docs/benchmark and spikes; provenance and
saved responses are not rewritten. Pre-expansion mixed-model results showed identical
Grounded and Grounded-plus-Guard results, not incremental guard benefit. Deterministic
fixtures establish extraction behavior only, not model quality or production ROI.

## Validation

CI runs Release backend build/tests with TURTLE_LIVE=0, plus Node 24.19.0 npm ci,
Vitest, TypeScript checks and production Next.js build. Live smoke is opt-in and
returns early without TURTLE_LIVE=1; an offline pass is not live validation.
See docs/REVIEW_HARDENING_VALIDATION.md for this change's exact evidence and limits.
Historical docs/design plans are snapshots, not the current behavior contract.

## Isolation follow-on

The separate Worker project and tools/worker_runtime.py provide a network-denied,
resource-bounded sealed-archive path. Model credentials stay outside workers.
Web admission defaults closed; explicit trusted-local access requires a key,
repository/origin allowlists and loopback clients, with bounded scheduling and
retention. See docs/ISOLATION.md for enforced controls and pending acquisition,
image supply-chain and public deployment gates. Earlier host-path descriptions
above apply to the explicit trusted-local pipeline, not public hosting.
