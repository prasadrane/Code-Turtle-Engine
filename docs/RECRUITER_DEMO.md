# Citation verification: three-minute demonstration

## Run without credentials

Requires the repository's pinned .NET SDK and Node versions. From the root:

```bash
export TURTLE_LIVE=0
dotnet build samples/CodeTurtleEngine.CitationDemo -c Release
dotnet run --project samples/CodeTurtleEngine.CitationDemo -c Release --no-build -- --out web/src/data/citation-demo.json
dotnet test tests/CodeTurtleEngine.Council.Tests -c Release
npm --prefix web ci
npm --prefix web run build
npm --prefix web start
```

Open http://localhost:3000/citation-demo. The static route needs no backend,
provider, API key, public repository acquisition, or model calls. The saved JSON
is an authored fixture processed by Roslyn and the shipped TurtleShellGuard,
not an empirical model response. The Council test regenerates the report and
compares it with the saved JSON, including source hashes and symbol identities.
It prevents silently changing display evidence independently of the implementation.

## Recording script and interview explanation

| Time | Show | Explain |
|---|---|---|
| 0:00–0:25 | Fixture label and both source files | AI reviews can cite nonexistent APIs. I separate deterministic citation evidence from recommendation correctness. |
| 0:25–0:55 | Valid declaration signature; expand identity | Roslyn resolves a declaration and records its assembly and documentation ID. Extracted context records member-name membership, not a defect location. |
| 0:55–1:25 | Fabricated overload, then fabricated member | Run(bool) fails even though Run exists. Strip removes findings whose citations all fail. Original suggestions remain auditable. |
| 1:25–2:00 | Real but unrelated member | A real API can support a bad recommendation. Compilation existence alone cannot establish relevance. |
| 2:00–2:25 | Name-only overload set | Run names two overloads. The audit exposes both identities and does not claim an exact signature. |
| 2:25–3:00 | Current CI and evaluation plan | Offline tests demonstrate policy. Controlled model evaluation is separate; this playback is not an accuracy benchmark. |

Record at a readable desktop viewport; also check the five controls at a mobile
viewport. Do not label this as a live AI review. A recording has not been produced.
No public deployment or second-machine acceptance is established by adding this page.

Suggested project description:

> Built a compiler-grounded C# review prototype with auditable symbol-name and
> supported declaration-signature checks, explicit rejection policies, and an
> offline demonstration that exposes verification limits.

## Evidence and limits

- [CI](https://github.com/prasadrane/Code-Turtle-Engine/actions/workflows/ci.yml)
- [Verification contract](VERIFICATION_CONTRACT.md)
- [Signature implementation](../src/CodeTurtleEngine.Gatekeeper/CitationSignature.cs)
- [Reproducible fixture generator](../samples/CodeTurtleEngine.CitationDemo/DemoScenario.cs)
- [Controlled live evaluation plan](CITATION_EVALUATION.md)

Avoid claims of eliminating hallucinations, verifying bugs, improving review
accuracy, faster merges, or production readiness without corresponding evidence.
