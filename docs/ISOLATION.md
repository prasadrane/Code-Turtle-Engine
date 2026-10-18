# Untrusted processing boundary (preparation)

Public hosted review is disabled by default. `Access:Mode=Hosted` is also closed:
there is no implemented public acquisition broker or public deployment approval.
Code Turtle does not fall back to host clone/restore/MSBuild for an isolated request.

The executable isolation path is `tools/worker_runtime.py` with the separate
CodeTurtleEngine.Worker image. Its inputs are **sealed source tar files** and JSON;
not arbitrary mount directories or URLs. Only that tar file is mounted read-only.
The trusted host runs Docker; the worker never receives its socket, host/cloud/model
credentials, host HOME, Docker config, or host package caches. Model transport is
outside the worker; the preparation runner uses explicitly labeled stub/saved findings.

Controls enforced in the launch policy:

- Locally pinned immutable image ID, no runtime pull; non-root UID/GID 10001.
- Read-only root, all capabilities dropped, no-new-privileges, default seccomp.
- No network namespace connectivity (`--network=none`); proxy variables blanked
  **inside the worker only**. No approved external endpoints are enabled yet.
- One CPU, 1 GiB memory including swap cap, 64 tasks, 256 file descriptors.
- Separate 512 MiB /work and 64 MiB /tmp tmpfs; no other writable application mounts.
- Tar input at most 64 MiB, 10,000 entries; links/special files/path escapes rejected.
- Changed paths must exist and belong to loaded compilations; no silent omission.
- Two admission slots across host client processes; reject extra clients.
- Default 120-second worker lifetime, hard maximum 300 seconds; bounded output;
  forced container removal on failure, cancellation, overflow and timeout.

Restore runs inside the worker against /opt/offline-feed only. Approved packages
must be prepared by the operator and baked into the immutable image. Arbitrary
network NuGet restore, GitHub cloning, source acquisition authentication and package
allowlist proxying are **not enabled**. The isolated source-acquisition integration
and real NuGet paths remain release gates, rather than delegating to a credentialed
host. Source archive hash is verified by host and worker, and across analyze/audit.
It is not proof of a live PR revision.

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
Expiration/cancel/shutdown closes streams and cancels work; retention is in-memory,
not durable history. Clone has a 60-second deadline, exact SHA checkout verification,
argument-list construction and no HTTP redirects. Cleanup does not follow symlinks.
This host path is for operator-approved trusted repositories only.

User-visible failure messages omit raw source-controlled diagnostics, provider
errors and host paths. Worker failures disclose only safe stage/type or exit status.
No build log is treated as safe for untrusted users. Resource failures remain explicit.

Runtime fixtures distinguish configuration checks, worker canaries, and real-client
checks. A container plus these controls is still not proof against kernel/runtime
vulnerabilities. Public operation needs source/package broker policy, operator
configuration, deployment validation and the relevant real review matrix.
