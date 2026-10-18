# Controlled benchmark protocol (no collection performed)

Historical Claude and mixed-model checkpoints remain separate and unchanged. This
preparation adds no empirical answers and does not claim fresh model quality.
bench/controlled-run.template.json intentionally lacks model, endpoint, settings and
cost values. Do not fill missing samples with another model or authored/copied answers.

Freeze after confirming model access, authorizing collection and choosing budgets:

```bash
python tools/benchmark_protocol.py --spec <completed-run-spec.json> --root . \
  --commit <exact-clean-code-commit> --out work/frozen-run.json
```

An empirical freeze requires a clean exact Git checkout. It records commit, every
corpus/label file, the schema's declared source, rubric and harness hashes, model ID
and confirmation evidence, endpoint without credentials, generation settings,
reviewer groups, entries, repetitions, and sample/cost ceilings. Directories expand
to file hashes; missing files and escaping paths fail. Recheck verify() against
current files/commit before collection. Hashes alone do not prove model identity or
sample independence; retain provider receipts and external confirmation evidence.

No provider collector is implemented or invoked by these tools. A future authorized
collector must open a new `Ledger(manifest, durable_path)` or resume with `Ledger.load`,
then call Ledger.reserve(slot, prompt_bytes) **before** each request and
Ledger.finish(ticket, outcome, answer_bytes, ...) afterwards, retain original independent raw answers atomically. Reservations and outcomes are
atomically persisted by the ledger before returning; never overwrite a previous run. Reservations consume attempt and
worst-case-cost budget, including failed/cancelled attempts. Pending reservations
remain visible after interrupted collection. Actual unknown costs remain unknown;
an observed cost overrun stops further reservations. The cost ceiling is an operator
reservation policy, not an invented provider price or a guarantee against provider
billing overruns. `record` is only a convenience for already saved offline fixtures.

Slots include entry, repetition, reviewer group, persona and source/grounded arm.
Retries cannot change prompt hashes. Guarded output derives from the exact same
grounded answer hash and makes no additional model request. Save original outputs;
never overwrite raw arms with their stripped copies. Ledger provenance rejects
changed manifests, model/harness/settings identities and mismatched guarded hashes.

```bash
python tools/benchmark_report.py --ledger <saved-ledger.json> --out work/report.json \
  --adjudication-template work/adjudication.csv
```

Reports preserve pooled counts/denominators, per-repetition and per-reviewer-group
metrics, citation coverage, invented/rejected/payload-unsupported citations,
precision/recall, missing slots, degraded/failed/cancelled/timed-out outcomes and
known versus unknown costs. Null/undefined metrics remain null. Current BenchScorer
now measures finding citation coverage; historical scores without that field stay
unknown, not zero. Legacy Unverified means absent from payload, not rejected by the
compilation oracle. Current RejectedCitations uses the same oracle as Invented.

Only complete comparable repetitions with scored source/grounded/derived-guard
outputs can support variation analysis; the additional variation flag requires
empirical category, explicit independence attestation and distinct collection
receipts. This flag is a gate for human review, not proof of independence. Preserve
all failed attempts and disclose any collection/harness changes. No quality claim
is inferred automatically from this metadata.

Manually adjudicate representative matched and unmatched findings. The CSV records
sampling reason, matcher label, reviewer disposition/rationale and missed issues;
matcher disagreement is not automatically a bad recommendation. Add an explicitly
controlled single-reviewer group to the **frozen** design before claiming council
benefit. Reports separate reviewer groups; no such comparison was collected here.
Synthetic corpus evidence does not establish generalization, business ROI, or faster
merges. New benchmark claims remain gated on authorized, complete collection.
