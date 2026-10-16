# Claude benchmark checkpoint

The Claude-only experiment is incomplete. The saved responses came from Claude
Code Sonnet subagents, according to the original session transcript (session
`6ec4653f-779a-4de5-b5b5-2354d721c6ee`). No Codex responses have been added.

## Saved progress

The local checkpoint is `bench/bench-out/claude-sonnet-checkpoint/` (gitignored).
It was copied from Claude's scratch directory, preserving the original prompts,
answers, and first-run report. It contains:

- 75 unique `prompts/<key>/prompt.md` files covering the 13 seeded entries.
- 75 first-repetition answers, 20 second-repetition answers, no third-repetition
  answers: 95 saved answers and 130 still needed for three repetitions.
- `inventory.json`: prompt SHA-256 hashes and available/missing repetitions.
- `prompts/provenance.json`: model, harness, session provenance (`reconstructed-from-session`), and SHA-256 hashes for every saved prompt and answer, verified automatically during replay.
- `replay-out/`: the original report and machine-readable results.
- `verified-replay/`: first-repetition results regenerated with strict replay.

The response files parsed successfully as JSON with a Findings array when this
checkpoint was created. Existing answers' provenance is labeled as reconstructed
from Claude's session transcript (`6ec4653f-779a-4de5-b5b5-2354d721c6ee`);
retroactive metadata verifies file integrity but cannot independently prove
sampling independence. The answers and inventory remain local; this document
does not make the checkpoint available in a fresh clone. Back up the checkpoint
directory along with the working repository.

The strict first-repetition replay completed with 13 entries, 39 arm rows, no
errors, no degraded councils, and no skipped entries. Its preliminary results
match the original report:

| Arm | Pooled invented citation rate | Pooled precision | Mean precision (defined entries) | Pooled recall | Mean recall (defined entries) |
|---|---|---|---|---|---|
| Naive | 14.2% (18/127) | 12.9% (8/62) | 12.9% (13/13) | 66.7% (8/12) | 66.7% (12/13) |
| Grounded | 0.0% (0/53) | 25.8% (8/31) | 31.5% (9/13) | 66.7% (8/12) | 66.7% (12/13) |
| Code Turtle | 0.0% (0/53) | 25.8% (8/31) | 31.5% (9/13) | 66.7% (8/12) | 66.7% (12/13) |

Under the deterministic label matcher, Grounded and Code Turtle matched 8 of 31
findings (**25.8% pooled precision**, ~2.0× Naive's **12.9% (8/62)**). Mean
precision (**31.5%**) averages only the 9 of 13 entries where Grounded produced
at least one finding. These are one-repetition results; sample variance remains
n/a.

## Resume with Claude

For each missing answer in `inventory.json`, start an independent Claude Sonnet
review that receives only that key's `prompt.md`. Follow its persona, input, and
output schema. Do not expose other answers, labels, scoring results, or repository
files. Save only the response JSON as `<repetition>.json` beside the prompt.
Keep the model and harness consistent with the original run and update
`prompts/provenance.json` with the new answer SHA-256 hashes. Do not overwrite
existing answers or copy a prior answer to fill a gap.

Once all three answers exist for every key, run from the repository root:

```powershell
dotnet run --project src/CodeTurtleEngine.Cli -- bench `
  --corpus bench/corpus.json --repeat 3 `
  --replay bench/bench-out/claude-sonnet-checkpoint/prompts `
  --out bench/bench-out/claude-sonnet-checkpoint/three-repetitions `
  --work bench/bench-out/claude-sonnet-checkpoint/work
```

Replay requires an explicit benchmark repetition number, so byte-identical prompts
across entries share the same repetition's answer. Missing responses fail rather
than cycle. Both live and replay runs return a nonzero exit code when any entry
is skipped or any row errors or degrades, while still writing the report.

The variance table includes only repetitions that are complete across all three
arms and all prepared entries, separating complete repetition counts from
non-null defined metric samples. It reports both pooled and mean metrics using
sample variance (n−1) rather than treating differences between seeded entries as
model variance. Fewer than two defined samples gives n/a variance. Replay timings
measure local processing and do not measure model inference.

## Export new prompts

```powershell
dotnet run --project src/CodeTurtleEngine.Cli -- bench `
  --corpus bench/corpus.json --repeat 1 `
  --record-prompts bench/bench-out/new-prompts
```

Recording and replay require no LLM credentials and cannot be combined. Recording
returns empty placeholder verdicts to advance the pipeline and emits no benchmark
report; its per-run artifacts are not empirical model results. Keep the original
prompt files for this checkpoint: changed prompts will have different keys.

The original experiment is a small synthetic corpus with project-authored labels.
Independent repetitions assess response variability, not generalization to real
repositories or other models. The one-repetition report is preliminary; no
three-repetition conclusion is currently supported.
