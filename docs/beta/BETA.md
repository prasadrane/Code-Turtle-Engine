# Bounded beta preparation

This package is an unpublished, framework-dependent Linux x64 preparation artifact.
It includes the CLI, separate worker binaries, offline example, tools and templates.
No live model, pilot or public deployment acceptance is claimed. Use only trusted
local repositories with the legacy CLI; use sealed-archive workers for untrusted
source. Public acquisition and public web release remain closed.

## Install and verify locally

Requirements: .NET SDK 10.0.100, Python 3.12+, Docker for isolated offline examples,
and Node 24.19.0 only if building the source dashboard. This bundle includes no SDK,
container daemon, model credentials or external package feed.

Verify the artifact checksum supplied separately, then inspect and extract the
operator-produced archive into a new directory. Do not extract arbitrary untrusted
tars. From the extracted package:

```bash
./turtle-beta --help
python tools/package_beta.py --verify <path-to-package.tgz>
```

For the published Codex Cloud environment, first source
/workspace/.cloud-setup/activate.sh. Local operators use their installed SDK path.
The wrapper fixes its working directory for rubric lookup and defaults TURTLE_HOME
to the package's state directory. Do not install in a world-writable shared path.

## Complete the offline example (no model credentials)

```bash
python tools/build_worker.py --dotnet-root <installed-dotnet-root> \
  --published worker --packages offline-feed --context <new-worker-build-directory>
python tools/fixture_matrix.py --image sha256:<returned-local-image-id> --out <new-results-directory>
```

The NuGet fixture case additionally needs the operator-built Turtle.OfflineFixture
package staged into the image with --packages offline-feed. If the package is absent,
the NuGet case correctly fails closed. The source checkout contains its harmless
project under tests/fixtures/OfflinePackage; no external feed is contacted.

Read example-review.md: it comes from an actual isolated fixture run with authored
stub findings. A fabricated member was stripped. Compilation-only existence does
not make an unrelated suggestion correct. The matrix checks archive identity,
compilation, audited output and cleanup; its timings are not model latency.

## Configure a trusted-local model review (requires separate authorization)

Only after choosing/authorizing a confirmed model, endpoint and budget, set
TURTLE_LLM_BASE_URL, TURTLE_LLM_API_KEY and the matching Llm__ModelRoles__fast/deep.
Use the configured protocol in cli/appsettings.json; do not bake keys into files.
Then ./turtle-beta review /absolute/path/to/trusted/repo --diff <baseline>.
Restore trusted target dependencies explicitly first. TURTLE_LIVE=0 disables the
opt-in smoke test only; it is **not** a provider-spending guard for the review command.
No such live review was run for this package.

The source web app defaults disabled. A trusted-local operator must explicitly set
Access__Mode=TrustedLocal, Access__ApiKey, Access__Origins__0 and
Access__TrustedRepositories__0. Use loopback only. Enter the local access key in the
browser; it exchanges for a short-lived HttpOnly cookie, not a URL token. All team
members sharing a bearer key share that bearer identity; browser-created jobs and
source artifacts are bound to their creating session. Artifacts expire with the
job and are delivered with `no-store`. Do not expose it with an unreviewed proxy.

## Run the local web UI

The public Vercel frontend provides the illustrative sample, not a hosted review
backend. The current API accepts loopback clients only. Run both services locally
under the same hostname (`localhost` for both, not a mix of `localhost` and
`127.0.0.1`), because the browser session cookie uses `SameSite=Strict`.

After configuring the authorized model endpoint above, configure the API in its
own terminal. For example, using Bash from the repository root:

```bash
export Access__Mode=TrustedLocal
export Access__ApiKey='<choose-a-local-access-key>'
export Access__Origins__0=http://localhost:3000
export Access__TrustedRepositories__0=owner/repo
dotnet run --project src/CodeTurtleEngine.Web --urls http://localhost:5000
```

In another terminal, run `cd web`, `npm ci`, and `npm run dev`, then open
http://localhost:3000. Enter the local access key under **Connect your local beta
server** and submit a PR from the allowed repository. Restore/build only trusted
target dependencies first. Model reviews use the configured provider and budget.

Development uses http://localhost:5000 when `NEXT_PUBLIC_API_BASE` is unset or
blank. For a local production preview (`npm run build` then `npm start`), set
`NEXT_PUBLIC_API_BASE=http://localhost:5000` in `web/.env.local` **before building**.
Without an explicit valid HTTP(S) origin, production shows local setup guidance
and disables live submission; the sample remains available. Next.js embeds public
variables at build time: changing this value requires a rebuild/redeploy, not just
restarting the server. Never put a key or token in `NEXT_PUBLIC_API_BASE`.

Do not point a public Vercel build at a loopback API. The browser would contact
the visitor's machine, and the current local session/access policy does not make
that a supported public service. To run a live review, open the local UI instead.

## Troubleshooting

- Cannot connect: check the API process and http://localhost:5000/health, the
  frontend's build-time API origin, browser CORS/mixed-content errors, and the
  exact hostname/port in the API origin allowlist. A changed port needs a matching
  allowlist entry. On the public frontend, use the sample or run both services locally.
- API 503: default/Hosted gate is closed; do not bypass the isolation gate.
- API 401/403: check local key, loopback peer, exact origin and repository allowlist.
- API 429: request, session, queue or retention capacity reached; wait or cancel.
- Worker startup failure: check Docker availability, image ID and public image file
  permissions; do not run the worker as root or mount host credentials to fix it.
- Restore/compile failure: supply approved offline packages and valid project paths.
  No external network fallback exists. Unlisted changed files stop the review.
- Timeout/OOM: inspect safe exit status and fixture logs; limits are deliberate.
- Empty report: no retained findings is not clean code; check audits and degraded status.
- Saved legacy demo: unavailable provenance is not current verification.

Pilot materials are templates only. Fill pilot-measurements.csv and feedback.md only
after participant authorization and relevant release/live gates. Keep unknown costs
and missing data explicit. Validate any deployment separately; do not claim market,
commercial, production or public-hosting readiness from this preparation package.
