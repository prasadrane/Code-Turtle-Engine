# Spike Results

Feasibility probes for the Code Turtle Engine MVP. Each spike records evidence and an unambiguous decision.

## Spike A — Roslyn compilation loader (.NET 10), 2026-09-08
- Target probed: `src/CodeTurtleEngine.Core/CodeTurtleEngine.Core.csproj` (Task 1 shell, 0 source files) and `spikes/msbuildworkspace_probe/msbuildworkspace_probe.csproj` (self-probe, 1 source file) as source-bearing control
- MSBuildWorkspace: OK, load ms=3000 (Core) / ms=2928 (self-probe), typeSymbols=0 (Core — project genuinely has no sources) / typeSymbols=1 (self-probe — synthesized `Program`), syntax trees 3 / 4, compilation errors=0, workspace-failed diagnostics=none
- Buildalyzer (if tried): not tried — MSBuildWorkspace succeeded with clean diagnostics
- Notes: Roslyn 5.9.0 (`Microsoft.CodeAnalysis.CSharp.Workspaces` + `Microsoft.CodeAnalysis.Workspaces.MSBuild`) on SDK 10.0.400, net10.0. `Workspace.WorkspaceFailed` event is obsolete in 5.9.0 (CS0618); use `RegisterWorkspaceFailedHandler`. `Compilation` exposes no `SyntaxTreeCount()` — use `SyntaxTrees.Count()`. First-run cold load ~3.7 s; warm ~3 s.
- Decision: CompilationLoader (Task 10) uses MSBuildWorkspace because it opened real csproj files on .NET 10, produced a Roslyn Compilation with correct syntax-tree/source inclusion (verified via self-probe) and zero workspace-failed diagnostics; Buildalyzer adds a dependency and a full-build roundtrip for no measured benefit.
