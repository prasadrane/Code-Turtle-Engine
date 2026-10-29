# Code Turtle: Production & Open Source Reviewer Roadmap (Reconciled)

## Executive Summary & Strategic Positioning

Code Turtle is an open-source, Roslyn-grounded pull-request reviewer for the .NET ecosystem.
Commercial competitors (CodeRabbit, Copilot, Greptile, Qodo) treat code review primarily as an LLM prompting problem over textual diffs and file trees. Consequently, they suffer from high rejection rates (~56% rejected in wild studies) and hallucinated symbol bindings.

Code Turtle’s core defensible moat is **compiler-verified truth**:
1. **Compilation-Grounded Findings:** Every cited symbol and type is verified against a loaded Roslyn semantic model.
2. **Deterministic .NET Analysis:** Zero-hallucination detection of public API breaking changes, dependency injection lifetime violations, and EF Core migration hazards before any model prompt is evaluated.
3. **Analyzer SARIF Triage:** Ingesting SDK NetAnalyzers (`CA` rules, security taint rules CA3001/CA3003) and community analyzers (Meziantou, Roslynator) on touched diff hunks, filtering out known false positives via LLM verification.
4. **Advisory, High-Precision Default:** Capped inline comments (≤5 per PR), strict diff-hunk intersection, and idempotent review updates.

---

## 1. Key Factual & Baseline Corrections

| Topic | Claude Code Roadmap Claim | Reconciled Reality & Codebase Fact |
|---|---|---|
| **21% Prec / 61% Recall Baseline** | Cites 21% precision / 61% recall as current engine baseline needing a guard overhaul. | **Historical mixed-model checkpoint** (`docs/benchmark/claude-sonnet-resume.md`) on 13 synthetic fixtures. Guarded and Grounded arms were identical (0.0% invented citations in both). The gap was a retrieval tradeoff: semantic payload reduced recall on raw-text issues (secrets, swallowed exceptions). |
| **ApiCompat & Surface Diffing** | Claims 2-commit API diffing is novel because existing tools require baseline files or packable libraries. | Microsoft already provides `Microsoft.DotNet.ApiCompat.Task` and `ApiCompat.Tool` for assembly-to-assembly validation on non-packable projects. Code Turtle's real value is **in-repo consumer re-binding** (identifying overload ambiguities at call-sites) and GH suggestion formatting. |
| **Taint Analysis in Free Tools** | Initially claimed free tools lack C# taint analysis; corrected to CA3xxx. | Corrected: NetAnalyzers ships CA3001, CA3003, CA3006 (disabled by default, intra-assembly). Interprocedural Roslyn analysis in MSBuild can cause severe build slowdowns; triage must be tightly bounded to touched entry points. |
| **Analyzer Licensing** | Proposed running SonarAnalyzer.CSharp alongside other tools. | **SonarAnalyzer.CSharp 10.x uses SSALv1**, explicitly prohibiting feeding analyzer outputs to external AI/LLMs. Must **not** be integrated. Use MIT/Apache-2.0 analyzers: NetAnalyzers, Meziantou, Roslynator, VS-Threading. |

---

## 2. Security Architecture: The Fork-PR Boundary

The primary vulnerability in GitHub Actions reviewers is the **"pwn request"**:

```
Fork PR (Untrusted) ──> [ pull_request ] ──> Read-only token, NO secrets. (Cannot call LLM API)
                    ──> [ pull_request_target ] ──> Has secrets + write token.
                                                ⚠️ DANGER: Checking out untrusted PR code
                                                   executes arbitrary MSBuild / source generators!
```

### The Architectural Invariant
Because loading an MSBuild solution evaluates `.csproj` XML, MSBuild tasks, and compiler plugins, **untrusted PR code must NEVER execute in the same process or environment that possesses `TURTLE_LLM_API_KEY` or repository write permissions.**

### The Two-Stage Pipeline

```
[Untrusted Stage 1: pull_request]
  - Checkout PR code (no secrets, read-only token)
  - Run MSBuildWorkspace + extract Roslyn payload & SARIF
  - Upload sanitized payload.json & diagnostics.sarif as signed build artifacts
        │
        ▼ (workflow_run event triggers)
[Privileged Stage 2: workflow_run]
  - No checkout of untrusted PR code
  - Possesses TURTLE_LLM_API_KEY & write GITHUB_TOKEN
  - Downloads & validates payload schema
  - Calls LLM gateway for triage / review
  - Applies TurtleShellGuard (diff-hunk intersection + code quote validation)
  - Posts batched PR review comments
```

---

## 3. Re-Sequenced Implementation Plan

### Phase 0: Loader Hardening & Eval Baseline (Weeks 1–3)
- **Goal:** Enable Code Turtle to load real-world open-source repositories without aborting on incidental workspace warnings or unrelated project build failures.
- **Tasks:**
  1. Amend `docs/VERIFICATION_CONTRACT.md`: Introduce the `Degraded` project status. Untouched projects in a solution with compile errors are flagged as degraded rather than aborting the entire solution load.
  2. Patch `MsBuildCompilationLoader.cs` (TDD): Catch project-level compilation errors and workspace diagnostic failures, isolate them to individual project records, and return a `LoadedCompilation` that includes degraded statuses.
  3. Curate a 60-PR dev evaluation corpus across 3 repositories (`dotnet/runtime`, `dotnet/roslyn`, `aspnetcore`) with human-labeled ground truth (pre-2025 to exclude bot contamination).

### Phase 1: Deterministic Core & Guard L1/L2 (Weeks 4–7)
- **Goal:** Maximize precision and zero-cost findings through Roslyn analyzers and strict guard boundaries.
- **Tasks:**
  1. **SARIF Triage Pipeline:** Ingest NetAnalyzers (CA3xxx security rules), Meziantou, and Roslynator diagnostics; intersect strictly with changed diff hunks.
  2. **TurtleShellGuard L1:** Enforce file and line intersection with the PR diff hunks (reject uncited or off-diff hallucinated line comments).
  3. **TurtleShellGuard L2:** Verbatim code quote verification for committable suggestions.
  4. **Deterministic API Surface Diff:** Integrate `Microsoft.DotNet.ApiCompat` logic to flag breaking public API changes without LLM involvement.

### Phase 2: Secure Delivery & Advisory Action (Weeks 8–11)
- **Goal:** Ship a frictionless, fork-safe GitHub Action that developers can install in under 5 minutes.
- **Tasks:**
  1. Implement the two-stage GitHub Actions workflow (`pull_request` extraction + `workflow_run` triage).
  2. Implement batched review posting: single review object with max 5–8 comments per PR.
  3. Idempotent state tracking: embed hidden SHA and finding fingerprints in the review walkthrough comment to prevent duplicate alerts or re-raising dismissed items.

### Phase 3: Agentic Roslyn Tooling Spike (Weeks 12–15)
- **Goal:** Objectively test whether an interactive agentic tool loop beats single-pass semantic payload extraction.
- **Decision Gate:** Prototype read-only Roslyn tools (`find_references`, `get_callers`, `go_to_definition`). If held-out evaluation does not demonstrate a statistically significant uplift in recall/precision or if latency exceeds 180s, keep the single-pass architecture and focus on analyzer breadth.

### Phase 4: DotNetReviewBench & 1.0 Hardening (Weeks 16+)
- **Goal:** Public release, third-party verifiable benchmarks, and multi-host breadth.
- **Tasks:**
  1. Publish **DotNetReviewBench** methodology, raw outputs, and public leaderboard.
  2. Azure DevOps iteration-aware PR pipeline integration.
  3. SLSA Level 3 container build attestation, SBOM, and signed GHCR releases.
