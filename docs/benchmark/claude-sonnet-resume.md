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
- `replay-out/`: the original report and machine-readable results.
- `verified-replay/`: first-repetition results regenerated with strict replay.

The response files parsed successfully as JSON with a Findings array when this
checkpoint was created. The answers and inventory remain local; this document
does not make the checkpoint available in a fresh clone. Back up the checkpoint
directory along with the working repository.

The strict first-repetition replay completed with 13 entries, 39 arm rows, no
errors, no degraded councils, and no skipped entries. Its preliminary results
match the original report:

| Arm | Pooled invented citation rate | Mean precision | Mean recall |
|---|---|---|---|
| Naive | 14.2% | 12.9% | 66.7% |
| Grounded | 0.0% | 31.5% | 66.7% |
| Code Turtle | 0.0% | 31.5% | 66.7% |

These are one-repetition results. Sample variance remains n/a. The tooling's
offline suite passed all 264 tests after the replay and variance fixes.

## Resume with Claude

For each missing answer in `inventory.json`, start an independent Claude Sonnet
review that receives only that key's `prompt.md`. Follow its persona, input, and
output schema. Do not expose other answers, labels, scoring results, or repository
files. Save only the response JSON as `<repetition>.json` beside the prompt.
Keep the model and harness consistent with the original run and record their
identity. Do not overwrite existing answers or copy a prior answer to fill a gap.

Once all three answers exist for every key, run from the repository root:

```powershell
dotnet run --project src/CodeTurtleEngine.Cli -- bench `
  --corpus bench/corpus.json --repeat 3 `
  --replay bench/bench-out/claude-sonnet-checkpoint/prompts `
  --out bench/bench-out/claude-sonnet-checkpoint/three-repetitions `
  --work bench/bench-out/claude-sonnet-checkpoint/work
```

Replay uses the explicit benchmark repetition number, so byte-identical prompts
across entries share the same repetition's answer. Missing responses fail rather
than cycle. Incomplete replay returns a nonzero exit code and retains diagnostics
in the report. A degraded council excludes that entire arm/repetition from the
variation calculation.

The variance table compares complete repetitions of the same prepared corpus,
using sample variance (n−1), rather than treating differences between seeded
entries as model variance. Fewer than two complete repetitions gives n/a variance.
Replay timings measure local processing and do not measure model inference.

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
