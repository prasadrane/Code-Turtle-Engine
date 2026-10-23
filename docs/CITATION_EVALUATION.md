# First controlled citation evaluation

Status: designed, not collected. No provider/model, dollar budget, external
repository acquisition, or new live results are implied by this document.

## Fixed first experiment

Use bench/citation-evaluation-corpus.json: hardcoded secret, sync-over-async,
path traversal, swallowed exception, boxing, and clean refactor. These are trusted
local synthetic fixtures, not a public-repository pilot. Use three independent
repetitions, the existing three personas, and two model-input arms (source and
grounded). This is **108 planned model attempts** before any retries. The guarded
arm derives from each exact grounded response with no extra model call. Do not
claim a council advantage: no single-reviewer control is included.

bench/citation-evaluation.template.json fixes these choices but deliberately
leaves model ID/confirmation, endpoint, generation settings, budgets, and independence
attestation unfilled. This cannot be frozen as an empirical run until completed.
The sample budget must cover 108 attempts and any explicitly permitted retries;
the maximum per-attempt reservation times the attempt ceiling must fit the total.

Freeze an exact clean commit with the existing protocol, then verify its hashes
before collection. Both run templates now include the signature matcher and
context/identity schema in harness provenance; old manifests do not validate this
new policy. Keep historical metrics unchanged and label new policy results separately.

```bash
python tools/benchmark_protocol.py --spec <completed-spec.json> --root . \
  --commit <exact-clean-commit> --out work/citation-evaluation/frozen.json
```

Prompt recording is already implemented and makes no provider calls:

```bash
dotnet run --project src/CodeTurtleEngine.Cli -- bench \
  --corpus bench/citation-evaluation-corpus.json --repeat 1 \
  --record-prompts work/citation-evaluation/prompts \
  --work work/citation-evaluation/repos --out work/citation-evaluation/recording
```

Recording uses the real project loader and may be unavailable in environments
that deny its build-host pipes. Empty recording responses are not empirical data.
Recording once exports unique prompts; it does not collect three independent
responses. The existing export/replay format must be mapped to entry/persona/arm
slots before an empirical collector is used. Do not bypass this by simply running
the CLI's live benchmark command: that path does not integrate the durable budget
ledger. A ledger-integrated collector/adapter remains to be implemented and tested
after the provider route is chosen. Follow [CONTROLLED_BENCHMARK.md](CONTROLLED_BENCHMARK.md).

## Outcomes to publish

| Question | Measure |
|---|---|
| Does the guard remove invalid citations? | Rejected / attempted citations, split by name-only versus explicit signature; distinguish fabricated, ambiguous, and unsupported syntax. |
| Does it wrongly reject valid citations? | Manually valid rejected citations / all manually valid citations, including supported and unsupported syntax categories. |
| Does filtering help findings? | Precision, recall, and retention before/after guard on identical grounded answers, with manual adjudication and raw denominators. |
| Do findings claim more than the evidence? | Uncited/advisory count, name-only overload sets, and citations with no matching extracted context. |
| Does the pipeline work? | Success/degraded/failure/timeout counts, provider latency, token usage, receipts, and actual or explicitly unknown cost. |

The legacy scorer's Invented field now means **rejected by the current resolver**.
It is not proof of fabrication: valid but unsupported signatures can also fail.
Manual adjudication must retain that distinction. Matcher label disagreement is
not automatically an incorrect recommendation. Report per-repetition counts,
paired guard deltas, missing slots, and variation; six synthetic cases do not
establish generalization or enterprise value.

Keep authored citation attacks separate from natural model outputs. They establish
policy capability, not the frequency of model mistakes.

## Acceptance before calling the experiment complete

1. Confirm provider/model access, settings, and authorized attempt/dollar limits.
2. Freeze the manifest and exported prompt-to-slot map on an exact clean commit.
3. Validate collection offline, including durable reservation before requests,
   no silent retry/model fallback, raw-response preservation, and receipts.
4. Collect independent answers within budget; preserve incomplete attempts.
5. Replay raw arms, derive guarded output from identical grounded answer hashes,
   adjudicate findings/citations, and publish results with limitations.
