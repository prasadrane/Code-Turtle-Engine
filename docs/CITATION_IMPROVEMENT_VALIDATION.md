# Citation evidence and demonstration validation

Base: f060ced512ed810dfd92e40367cb9771f0717bc3.

The original local implementation was committed as a69f343 in a temporary workspace.
Before GitHub publication, that workspace became unavailable. The source in this
branch was reconstructed from the conversation's recorded edits; it is a new commit,
not a recovered copy of that Git object. The saved demo JSON was reconstructed too;
its regression test must compare it against current compiler/guard output before
the displayed provenance is accepted.

## Previously recorded local checks

The earlier workspace recorded 51 focused oracle/signature tests and 35 Council
tests passing, plus 119 frontend tests, TypeScript, and production build. The full
backend attempt recorded 347 passes and 16 build-host pipe failures; the untouched
base reproduced the same 16 failed test names. Tooling recorded 19 passes and six
Docker-dependent skips. Those logs and files are currently inaccessible. These
counts describe the earlier run and are **not validation of this reconstructed commit**.

## Current acceptance

Use this branch's GitHub Actions run and PR check results for current validation.
The existing CI builds/tests the backend, checks the generated demo snapshot, runs
frontend tests/typecheck/build, and exercises worker canaries and the fixture matrix.
Record the actual outcome after the run; do not infer acceptance from configuration.

Signature matching is conservative declaration comparison, not call-site binding.
Unsupported generic substitutions can reject valid APIs. Payload context is name
membership, not proof of invocation or relevance. Unrelated real citations can
survive Strip. Fresh model quality remains unmeasured.

No provider calls, spending, new model responses, pilot activity, explicit deployment,
merge, second-machine acceptance, browser visual inspection, or screen recording
was performed during reconstruction. The benchmark design is prepared, not collected.

## Review follow-ups

Review of the reconstructed diff found that suffix normalization could turn invalid
Run?(int)/Run[](int) heads into valid signatures. Explicit member heads now preserve
those suffixes and reject, with dedicated regressions. Context lookup also handles
dots inside generic arguments without losing the member name. The demo's reproduction
link is pinned to a reachable source commit so it works before merge.

The first remote run on bcba1d1 passed backend (364 tests), frontend (119 tests,
typecheck and build), and tooling. Those results precede the review fixes; use the
new head's CI for acceptance. No browser visual inspection or live model evaluation
has been added by these follow-ups.
