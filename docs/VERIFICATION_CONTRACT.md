# Citation existence contract

This contract applies to CLI, web, and the derived guarded benchmark arm.
Product review stops on workspace failures or any project compilation error. There
is no partial-compilation mode. Warnings alone permit review. Missing references
and unresolved dependencies cannot produce a completed review. Project compilations
keep their own references, parse options, and semantic models; trees are not merged.

A citation's audit evidence is `Payload` when its exact ordinal name occurs in the
full, untrimmed `ResolvedSymbols` and the compilation oracle accepts it;
`Compilation` when only the oracle accepts it; otherwise `Rejected`. A library
caller without an oracle uses exact payload membership only. Namespace prefixes
are not evidence. Duplicate fully qualified source types across projects are
ambiguous and rejected. Error types and members with unresolved signature types
are excluded. `Verified` describes citation existence only.

Existence is type/member-name granularity. SymbolOracle normalizes generic arguments,
parameter lists, aliases, nullable and array suffixes, and supports inherited members.
It does **not** verify an exact overload, argument binding, location, changed-code
relevance, prose, severity, or recommendation correctness. A real unrelated symbol
can have `Compilation` evidence; that does not ground a recommendation in changed code.

| Citations | Strip advisory (default) | Strip strict | Flag advisory or strict |
|---|---|---|---|
| All resolve | Retain | Retain | Retain originals |
| All rejected | Drop finding | Drop finding | Retain with rejected audit |
| Mixed | Retain only resolved citations | Same | Retain all, audit each |
| None | Retain as uncited advisory | Drop | Retain as uncited advisory |

`RequireCitationsInStripMode` affects Strip only. Flag never implies verification.
SSE `rejected` counts failed existence checks; `stripped` is zero in Flag mode.
`GuardAudit` / `audit.json` contain every attempted citation, including rejected ones.
`FindingAudit` / `finding-audit.json` retain original merged findings, their retention
and status (`UncitedAdvisory`, `RejectedCitations`, `CitationExistenceOnly`). Raw persona
outputs remain in `Personas` / `verdicts.json`; arbitration can remove duplicates
before guard audit. Retained findings may have citations removed without modifying
original persona outputs. No findings means no retained findings, not clean code.

Web persona streams are provisional and unaudited. Completion replaces the stream's
working findings with synthesized results. Raw persona tabs explicitly label removed
findings and show citation audit evidence where available. A true legacy `Verified`
boolean without `Evidence` is displayed as evidence unavailable, not as current
verification. Old demos remain playable without inventing provenance.

BenchScorer separately measures payload membership and compilation existence; neither
is a correctness score. Naive and Grounded preserve original Flag findings. The
CodeTurtle arm derives Strip results using the same oracle without another model call.
Historical metrics are pre-expansion evidence and do not validate new behavior.
