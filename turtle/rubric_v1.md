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
- Allocations & Performance: Missing ConfigureAwait(false), unawaited tasks (fire-and-forget), sync-over-async (.Result/.Wait()), boxing allocations, LINQ closure captures, thread-unsafe collections (e.g. non-thread-safe Dictionary across tasks), and LOH allocations.
- Security: SQL injection (dynamic interpolation or concatenation into DbCommand/queries), path traversal (unvalidated Path.Combine or absolute path overrides), hardcoded credentials/secrets, and authentication/authorization bypass.
- Idiomatic: Modern C#, DI lifetimes (captive scoped/transient dependencies in singletons), service locator anti-patterns (IServiceProvider), swallowed/empty exception handling, and clean-architecture boundaries.
