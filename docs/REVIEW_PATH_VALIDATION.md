# Review path matrix: fixture evidence and pending live checks

The preparation runner never contacts a provider or GitHub. Its inputs are sealed
local fixture tar files, authored findings and a pinned worker image. Analyze and
audit run in separate disposable workers over the same archive hash. The audit
uses the shipped Arbiter and TurtleShellGuard, not a Python reimplementation.
Raw authored findings are retained separately from stripped output.

```bash
python tools/fixture_matrix.py --image sha256:<locally-built-image-id> --out work/fixture-matrix
python tools/known_pr.py --out work/known-pr-preflight.json
```

Fixture matrix cases actually executed in this cloud environment:

| Case | Evidence |
|---|---|
| Normal C# | Isolated restore/compile, payload and guarded authored mixed citations |
| NuGet-dependent fixture | Locally authored Turtle.OfflineFixture 1.0.0 baked into the image feed; not a live external NuGet check |
| Multi-project | Referenced projects compiled in their own contexts; selected paths and archive identity checked |
| Model failure | Stub throws after isolated compilation; no final verdict or invented recovery |
| Invalid/inaccessible PR and non-.NET | Mocked GitHub metadata in WebReviewPipelineTests; no public repository contacted |
| Model cancellation, timeout and cleanup | HostedControlsTests and WorkerCanaries; processes/streams cancelled and disposable workers removed |
| SSE and UI | Backend provisional events, safe failures, browser cookie authentication/cancellation and frontend reconciliation regressions |

The report under work/fixture-matrix/report.json records archive hashes and actual
fixture processing times. These are worker/stub timings, not provider latency.
No live paths were exercised. CLI trusted-local .NET paths retain their existing
integration suite; tools/isolated_review.py is the sealed-archive command-line
preparation surface. The public web pipeline remains disabled; it does not claim
to route remote repositories through the isolated offline worker.

Known-PR configuration is tools/known_pr.json. Fill in an actual PR, exact base/head
SHAs, confirmed model/route/settings, sample/cost ceilings, isolation report and
source/package acquisition policy only after separately authorizing those actions.
The preflight returns explicit missing gates and an empty live_paths_exercised list.
It has no live execution mode. It does not spend or accept an execute_live switch.

Before any future live run, validate the broker and compare fetched HEAD with the
expected revision, record the complete changed-file inventory, stop on missing
compilation paths, record final audits/artifacts and test cleanup. GitHub file
pagination now loads up to the API's bounded page limit and rejects a mismatch
with changed_files instead of silently reviewing a truncated list. Trusted-local
clone fetches an exact SHA and checks HEAD after checkout, with no HTTP redirects.
These code paths have fixture coverage, not real-client release acceptance.
