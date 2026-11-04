# Untrusted processing boundary (preparation)

Public hosted review is disabled by default. `Access:Mode=Hosted` is also closed:
there is no implemented public web acquisition broker or public deployment approval.
Code Turtle does not fall back to host clone/restore/MSBuild for an isolated request.

The executable isolation path is `tools/worker_runtime.py` with the separate
CodeTurtleEngine.Worker image. Its inputs are **sealed source tar files** and JSON;
not arbitrary mount directories or URLs. Only that tar file is mounted read-only.
The trusted host runs Docker; the worker never receives its socket, host/cloud/model
credentials, host HOME, Docker config, or host package caches. Model transport is
outside the worker. The preparation runner uses explicitly labeled stub/saved findings;
the opt-in GitHub runner uses the trusted council command described below.

Controls enforced in the launch policy:

- Locally pinned immutable image ID, no runtime pull; non-root UID/GID 10001.
- Read-only root, all capabilities dropped, no-new-privileges, default seccomp.
- No network namespace connectivity (`--network=none`); proxy variables blanked
  **inside the worker only**. No approved external endpoints are enabled yet.
- One CPU, 1 GiB memory including swap cap, 64 tasks, 256 file descriptors.
- Separate 512 MiB /work and 64 MiB /tmp tmpfs; no other writable application mounts.
- Tar input at most 64 MiB, 10,000 entries; links/special files/path escapes rejected.
- Changed paths must exist and exactly match trees inside the sealed source root;
  suffix matches from other directories cannot satisfy compilation coverage.
- Two admission slots across host client processes; reject extra clients.
- Default 120-second worker lifetime, hard maximum 300 seconds; bounded output;
  forced container removal on failure, cancellation, overflow and timeout.

Restore runs inside the worker against /opt/offline-feed only. Approved packages
must be prepared by the operator and baked into the immutable image. Restore
suppresses `RestoreAdditionalProjectSources` so project/props files cannot append
feeds. Arbitrary network NuGet restore, authenticated private acquisition and package
allowlist proxying are not enabled. The GitHub runner acquires public Git objects on
the trusted host, without checking out or evaluating PR code, then passes one sealed
archive to the worker. Source archive hash is verified by host and worker across
Analyze/Audit; provenance separately records the PR head, base and merge-base SHAs.

The image builder uses explicitly selected installed SDK directories and public
system libraries, with normalized image permissions and a runtime file hash manifest.
It copies no host configuration, HOME or credentials. The registry SDK pull in this
cloud environment returned Forbidden; this offline local-runtime image can exercise
controls, but is not a claim of a reproducible, patched production base distribution.
A production image supply-chain/patching review remains required.

The trusted-local legacy web path requires an explicit `Access:Mode=TrustedLocal`,
loopback client, server API key, explicit repository allowlist and allowed browser
origins. It is a single-team authorization boundary, not multi-tenant identity.
Forked head repositories are rejected before the host cloner, even when their base
repository is allowlisted. The key is exchanged for a 15-minute HttpOnly SameSite=Strict cookie for EventSource;
keys never enter URLs. CORS uses exact origins and credentials. Hosted mode cannot
select the host pipeline. Team members sharing the key share access to all retained
jobs. Do not expose this loopback path via an unreviewed reverse proxy.

Kestrel bounds connections, request body size, header/keepalive timeouts; all job
API routes share the request limiter. Web admission is limited to 30 requests/minute by default (configurable 1–1000),
two running reviews and eight queued reviews. Running tasks have a two-minute
cancellation deadline. Jobs expire after 30 minutes with at most 128 retained jobs;
SSE channels are bounded to 256 events and backpressure respects cancellation.
Only one stream subscriber can consume a job at a time; a competing connection
receives HTTP 409. Reconnecting to a completed job returns its retained terminal
result. The browser can recover a stored completed result after transport loss;
partial findings are never promoted to a completed report.
Expiration/cancel/shutdown closes streams and cancels work; retention is in-memory,
not durable history. Clone has a 60-second deadline, exact SHA checkout verification,
argument-list construction and no HTTP redirects. Cleanup does not follow symlinks.
This host path is for operator-approved trusted repositories only.

Trusted-local web dependency preparation requires `WebDependencies:Enabled=true`
and explicit absolute local package directories in `WebDependencies:OfflineSources`.
Restore has a default 45-second deadline (configurable 1–60 seconds), drains output
without exposing raw diagnostics, and stops review on failure. This capability does
not enable remote package feeds or public hosted acquisition. Additional project
sources are suppressed here too. CLI review still requires separately prepared
dependencies. `tools/prepare_web_offline.py` stages only selected package archives
from an explicit existing local cache; see the beta setup guide.

## GitHub PR workflow

Stage 1 is a secret-free notification with no PR checkout or artifact extraction.
Stage 2 defaults off. After deploying the trusted workflow to the default branch,
configure these repository settings:

- Variable `CODETURTLE_REVIEW_ENABLED=true` to opt in.
- Variable `TURTLE_LLM_BASE_URL` and secret `TURTLE_LLM_API_KEY` for the trusted
  CLI configuration's protocol/model (currently Anthropic Messages / qwen3.8-flash).
- Optional variable `CODETURTLE_REVIEW_PROJECT` for a relative project or solution
  path; default `CodeTurtleEngine.sln`.

The ephemeral Ubuntu runner installs SDK 10.0.100 into a fresh directory, publishes
trusted reviewer/worker binaries, restores the trusted solution (including tests),
and bakes packages from that trusted restore into
the worker's offline feed. PR-specific packages absent from that feed fail closed.
Acquisition currently supports open public github.com PRs only. Ambiguous PR
associations, stale head revisions, private repositories and unsafe paths are rejected.
Deleted C# files are excluded from head extraction; a PR without changed head-side
C# files produces an explicit no-review report without model calls.

`tools/github_isolated_review.py` obtains PR metadata using a read-only token, acquires
pinned Git objects with isolated Git configuration, and seals the head archive once.
It runs worker Analyze, trusted `council --analysis <analysis.json> --out <findings.json>`,
then worker Audit against that archive. The council uses the bundled rubric and
application-relative configuration; its raw findings require Audit before use.
The GitHub token is removed before model transport, and workers receive no model
credentials. Stage 2 has read permissions only and retains review, findings, coverage,
audit, persona participation and provenance artifacts for seven days. Reports label
degraded councils when fewer than three personas succeed. It posts no PR comments and consumes
no PR-produced payloads.

Local orchestration tests and workflow lint validate this implementation. The new
workflow has not been run remotely in this change session. On November 4, 2026,
Docker Desktop crashed during `dockerInference` startup before its Linux daemon
became available, so current container runtime checks could not run locally.
The configured `TURTLE_LLM_BASE_URL` and `TURTLE_LLM_API_KEY` were absent; no live
provider review was performed. These are validation limits, not deployment acceptance.

User-visible failure messages omit raw source-controlled diagnostics, provider
errors and host paths. Worker failures disclose only safe stage/type or exit status.
No build log is treated as safe for untrusted users. Resource failures remain explicit.

Runtime fixtures distinguish configuration checks, worker canaries, and real-client
checks. A container plus these controls is still not proof against kernel/runtime
vulnerabilities. Public operation needs source/package broker policy, operator
configuration, deployment validation and the relevant real review matrix.
