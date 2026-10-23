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

Name-only citations establish type/member-name existence, not an overload. Audits
optionally include Resolution: NameOnly, Signature, or Rejected, with assembly
identity, Roslyn documentation ID, and display text for resolved declarations.
Different assembly identities with the same fully qualified type name fail closed.
A name-only overload set exposes all matching declarations and never claims a
selected signature. Legacy audits without Resolution keep their earlier limits.

Explicit parameter lists are conservatively compared to declaration signatures,
including parameter types, array rank and ref/out/in modes. C# primitive aliases
and global:: are normalized. Method generic parameters must use the declaration's
parameter names; inference, constructed generic method substitution, constructed
generic owner substitution, tuple signatures, function pointers and invocation
binding are not supported. Unsupported or malformed explicit signatures reject;
rejection is not by itself proof of a fabricated API. Extension methods can be
cited by their static declaration signature, not as a verified reduced invocation.
Inherited/interface declarations can resolve, but this is not a call-site lookup.

PayloadContexts separately lists file/member ranges whose extracted ResolvedSymbols
contain the citation's member name (an explicit signature is reduced to its name
for this lookup only). It does not establish that the exact overload was referenced,
that a finding's claimed location matches, or that code on changed lines uses it.
Extraction includes members in changed files, and metadata may include declarations,
field and constructor types as well as invocations. An empty context list means no
matching extracted name, not proof of irrelevance. Payload still means exact raw
name membership in the full payload plus oracle acceptance; a signature citation
can have Compilation evidence and separately have name-level payload context.

Neither identity nor context verifies argument binding, defect location, changed-code
relevance, prose, severity, or recommendation correctness. A real unrelated symbol
can have Compilation evidence and survive Strip. Prompt trimming limits are unchanged.

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
