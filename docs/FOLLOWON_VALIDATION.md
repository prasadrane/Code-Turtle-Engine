# Steps 8–11 preparation: validation and release gates

Validated in the existing cloud workspace on 2026-10-18. Branch:
`codex/review-hardening`; starting commit `ec701ba` remains in the ancestry.
Implementation commits: `e30f474` (isolation/admission), `2447c29` (review fixtures),
`0f74217` (benchmark protocol), `9c2cf41` (beta package/CI).
This report supplements the historical steps 1–7 validation report.

## Implemented and exercised

| Step | Implemented preparation | Evidence and limits |
|---|---|---|
| 8 | Disposable sealed-source Docker worker; offline restore; separate credentialed transport boundary; archive validation; resource, output, time and concurrency limits; cleanup. Hosted admission defaults closed. Explicit trusted-local authentication, repository/origin allowlists, rate limits, cancellation, bounded queue, retention and safe errors. | Actual local Docker canaries exercise filesystem/credential isolation, no network, cgroup limits, build targets, exhaustion and cleanup. HTTP controls use in-process end-to-end fixtures. Trusted-local host processing is not an isolation claim. Public acquisition and hosted processing remain disabled. |
| 9 | Isolated analyze/audit matrix and known-PR configuration preflight. Original responses and source hash identity retained. | Normal C#, locally authored NuGet package, multi-project and injected model failure exercised with authored stubs. Invalid/inaccessible/non-.NET, UI/SSE and cancellation checks use backend/frontend fixtures. No live public PR/provider/client validation performed. The known-PR tool is preflight-only, not an enabled live executor. |
| 10 | Frozen provenance and hashes; comparable slots/repetitions/reviewer groups; durable reservation and budget accounting; guard derived from identical grounded answers; failure accounting; nullable coverage metrics; manual adjudication support. | Offline protocol/scorer regressions passed. No new model answers, empirical benchmark, model ID, cost or independence evidence invented. Historical Claude and mixed-model evidence remains intact. |
| 11 | Deterministic local package, installation/configuration/troubleshooting, fixture example, empty pilot measurement and feedback templates. | Package extracted, manifest verified, CLI help and packaged isolated fixture matrix exercised. No publication, participant contact, pilot evidence or deployment validation. |

The worker uses pinned local image
`sha256:3afaadb5281769e98a5ea668ff70d23da18e2d25085a1827fc7087bab0eba25c`.
The official SDK registry pull returned Forbidden. Instead, the offline image builder
copied installed SDK 10.0.100 and selected public runtime libraries into a scratch
image with synthetic account files and a hashed runtime manifest. Host configuration,
HOME and credentials were not copied. This validates the local runtime controls;
production image provenance, patching and supported-host validation remain gates.

## Validation outcomes

All .NET commands sourced `/workspace/.cloud-setup/activate.sh`; SDK 10.0.100,
Node 24.19.0, npm 11.9.0, Python 3.12.14. `TURTLE_LIVE=0` throughout.
That variable controls test selection, not spending authorization.

- Release solution build: passed, zero warnings/errors (`work/followon-final-build.log`).
- Full backend offline suite: **331 passed**, zero failed/skipped
  (`work/followon-final-tests.log`, TRX under `work/followon-results`).
- Frontend: **75 passed**, TypeScript check and production build passed
  (`work/browser-auth-{tests,types,build}.log`). No concurrent development server build.
- Python policy/protocol/package and actual Docker canaries: **21 passed**, no skips;
  `work/followon-tooling-final.log`. Runtime tests require the explicit prepared image;
  an ordinary image-less invocation skips those five tests and is not runtime evidence.
- Installed package fixture matrix: three successful review cases plus injected
  model failure with no verdict; zero live calls and no live paths
  (`work/beta-installed-fixture-matrix/report.json`). Recorded timings are fixture
  worker timings, not provider or production latency.
- Changed source files satisfy the 300-line limit. CI YAML parses. GitHub CI was
  configured, not executed remotely.

Representative commands, from the repository root:

```bash
source /workspace/.cloud-setup/activate.sh
export TURTLE_LIVE=0
dotnet build CodeTurtleEngine.sln -c Release --no-restore -m:2
dotnet test CodeTurtleEngine.sln -c Release --no-build -m:2
TURTLE_WORKER_IMAGE="$(cat work/worker-final-image/image-id.txt)" \
  python -m unittest discover -s tests/tooling -v
python tools/package_beta.py --verify work/codeturtle-beta-preparation.tgz
```

The beta artifact was built from `9c2cf41`; its internal file manifest is
`work/beta-package-manifest.json`. Checksums establish integrity, not publisher
authenticity. The package is a preparation artifact, not a release approval.
Two independent package builds produced identical SHA-256:
`ef555afdf13d0f626ea9e9899f526ab9da055e20a2a52e9e25e3a78b18317b50`.
Source verification was used because graph/codebase-memory tools were unavailable.

## Pending acceptance and explicit boundaries

An approved source/package acquisition broker is still required before untrusted
public repositories can enter the worker. No arbitrary Git/NuGet egress or host
fallback was enabled. Public hosted access remains fail-closed. Actual Kestrel,
reverse-proxy/TLS, deployment, tenant authorization, production image supply chain,
resource policy and recovery requirements need validation on the intended host.

Real CLI/web review paths require the intended PR and revision, confirmed model and
endpoint/access, sample/cost budget, and the isolation/acquisition gate. Benchmark
collection additionally requires a frozen complete manifest, comparable repetitions
and manual adjudication. Pilot/deployment work requires hosting and participant
details plus separate authorization. No provider calls, spending, new empirical
responses, deployment, outreach, merging or commercial claims occurred here.

The earlier push returned HTTP 403. Commits, local diff/bundle and package are
retained in the cloud workspace; no remote draft PR is claimed. No credentials
were requested, transferred or used to bypass that permission boundary.
