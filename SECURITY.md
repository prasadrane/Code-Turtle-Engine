# Security Policy

## Supported versions

Only the current `main` branch. Release tags (e.g. `v0.1.0-mvp`) are point-in-time snapshots and do not receive security updates.

## Reporting a vulnerability

Use GitHub's **private vulnerability reporting**: open the repository's **Security** tab and click **"Report a vulnerability"**. Do not open a public issue for security reports.

## Scope and design notes

Code Turtle Engine is a local CLI tool with no network-facing surface. It does not run untrusted code; it compiles and analyzes a repository you point it at, using Roslyn/MSBuild on your machine — treat target repositories accordingly (compiling a repository executes its build logic).

Secrets handling: LLM credentials are read from environment variables only (`TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`). Configuration files reference env-var *names*, never literal values. If you find a real credential committed anywhere in this repository's history, report it privately as above so it can be rotated and purged.

No security guarantees are claimed beyond the mechanisms described.
