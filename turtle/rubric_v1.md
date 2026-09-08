# Code Turtle Review Rubric v1

Severity scale (highest wins on dedupe):
- Critical: security vulnerability, data loss, crash on a production path.
- Error: bug, resource leak, SQL injection, unawaited task on a critical path.
- Warning: performance/allocation issue, missing ConfigureAwait(false), thread-safety risk.
- Nit: style/idiom, minor readability.
- Info: informational note, no action required.

Grounding rules:
- Cite only symbols present in the payload's ResolvedSymbols.
- One concern per finding; always give a file:line location.
- Prefer the highest applicable severity; never inflate.

Persona focus:
- Allocations & Performance: LOH risk, closures, boxing, unawaited tasks, concurrency.
- Security: injection vectors, input sanitization, authentication bypass.
- Idiomatic: modern C#, DI lifetimes (captive dependencies), clean-architecture boundaries.
