# Code-Turtle-Engine MVP Implementation Plan

> **Historical implementation plan (TDD work-order).** Some stack details (NSubstitute, Semantic Kernel, model roles) were superseded during implementation; as-built reference: [ARCHITECTURE.md](../../../ARCHITECTURE.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local .NET 10 CLI that reviews a C# repo's diff by grounding an AI agent council in deterministic Roslyn facts, with a guard that enforces zero-hallucination output.

**Architecture:** `MSBuildWorkspace` builds a full-repo `Compilation`; a `CSharpSyntaxWalker` + `SemanticModel` turn changed files into a minified JSON semantic payload; three Semantic Kernel personas deliberate over the payload in parallel; an Arbiter synthesizes a markdown review; the Turtle Shell guard verifies every cited symbol against the compilation before output. LLM access goes through a gateway over Alibaba Bailian's OpenAI-compatible endpoint (Qwen) with ordered fallback, Polly resilience, and a mock client for offline tests. The pipeline fails closed: no compilation, no payload, no review.

**Tech Stack:** .NET 10 (C# 13), Roslyn (`Microsoft.CodeAnalysis.CSharp.Workspaces`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`), LibGit2Sharp, `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI`, Semantic Kernel, Polly v8, System.CommandLine, xUnit + NSubstitute.

**Spec:** `docs/superpowers/specs/2026-09-08-code-turtle-engine-design.md`

## Global Constraints

- Target framework `net10.0` for every project; C# language version `latest` (13). Nullable enabled. Implicit usings enabled.
- Namespace root `CodeTurtleEngine`. Assembly name = project name.
- **File-size invariant: ≤300 lines per source file.** Split when exceeded.
- **Secrets never committed.** `appsettings.json` references env vars by name; values resolve from environment at runtime. Env vars: `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`, `TURTLE_HOME` (default `~/.turtle`).
- **Model role map:** `deep = qwen3.8-max` (Arbiter, Security Auditor, Idiomatic Architect); `fast = qwen3.8-flash` (Allocations & Performance scan). Mapping lives in config, not code.
- Add NuGet packages with `dotnet add package <id>` (resolves latest stable). `System.CommandLine` is prerelease: add with `--prerelease`.
- **Offline-first tests.** No network in the default suite. Live Bailian smoke gated by env `TURTLE_LIVE=1`, kept in a separate `LiveTests` class excluded by default.
- **Fail-closed:** if the target repo does not compile, abort before the council (no payload ⇒ no review).
- **Payload-only to LLM:** raw source is never sent to a model; only the minified JSON semantic payload.
- **Turtle Shell guard default mode: `Strip`** unresolved symbol citations (config can switch to `Flag`).
- TDD: write the failing test first, watch it fail, implement minimal code, watch it pass, commit. Conventional Commit messages (`feat:`, `test:`, `chore:`, `docs:`).
- Repo is already a git repository (initialized during design). Commit at the end of every task.

## File Structure (map)

```
CodeTurtleEngine.sln
Directory.Build.props                         # shared TFM, nullable, langversion, TreatWarningsAsErrors
global.json                                   # (optional) pin .NET 10 SDK band
src/
  CodeTurtleEngine.Core/
    Domain.cs                                 # enums Severity, PersonaRole, GuardMode
    Payload.cs                                # RoslynPayload, FileFacts, MethodFacts
    Findings.cs                               # ReviewFinding, PersonaVerdict, GuardResult, CouncilVerdict
  CodeTurtleEngine.Llm/
    ErrorClassifier.cs                        # LlmErrorKind + pure ClassifyStatus(int,string?)
    LlmExceptions.cs                          # ProviderException, ModelException
    LlmOptions.cs                             # route/model-role config bound from appsettings+env
    ChatClientFactory.cs                      # IChatClientFactory + Bailian OpenAI-compatible client
    MockChatClient.cs                         # canned IChatClient for offline tests
    LlmGateway.cs                             # ordered fallback + Polly resilience
    StructuredChat.cs                         # IStructuredChatClient: schema request + JSON parse + degrade path
  CodeTurtleEngine.Gatekeeper/
    DiffProvider.cs                           # LibGit2Sharp changed *.cs files + hunks
    CompilationLoader.cs                      # MSBuildWorkspace (or Buildalyzer per spike) -> Compilation
    CompilationException.cs
    TurtleSyntaxWalker.cs                     # CSharpSyntaxWalker node collection
    SemanticAnalyzer.cs                       # SemanticModel: ConfigureAwait/boxing/unawaited/casts/captive-DI/call-graph
    PayloadGenerator.cs                       # builds RoslynPayload + ResolvedSymbols
  CodeTurtleEngine.Council/
    RubricLoader.cs                           # loads turtle/rubric_v1.md
    PromptBuilder.cs                          # persona system+user prompts from payload+rubric
    PersonaRunner.cs                          # parallel Task.WhenAll persona calls -> PersonaVerdict
    Arbiter.cs                                # dedupe/resolve/synthesize -> markdown + CouncilVerdict
    TurtleShellGuard.cs                       # verify cited FQNs vs Compilation -> GuardResult[]
  CodeTurtleEngine.Cli/
    Program.cs                                # System.CommandLine root + `review` command
    ReviewPipeline.cs                         # orchestrates diff->compile->walk->payload->council->guard->output
    TurtleOptions.cs                          # root options (paths, thresholds, guard mode)
    ArtifactWriter.cs                         # writes payload.json/verdicts.json/review.md/audit.json under TURTLE_HOME
    appsettings.json                          # committed config, env-referenced secrets
tests/
  CodeTurtleEngine.Core.Tests/PayloadJsonTests.cs
  CodeTurtleEngine.Llm.Tests/{ErrorClassifierTests,ChatClientFactoryTests,LlmGatewayTests,StructuredChatTests}.cs
  CodeTurtleEngine.Gatekeeper.Tests/{DiffProviderTests,CompilationLoaderTests,WalkerTests,SemanticAnalyzerTests,PayloadGeneratorTests}.cs
  CodeTurtleEngine.Council.Tests/{RubricLoaderTests,PromptBuilderTests,PersonaRunnerTests,ArbiterTests,TurtleShellGuardTests}.cs
  CodeTurtleEngine.Cli.Tests/{ReviewPipelineTests,ArtifactWriterTests}.cs
  CodeTurtleEngine.Integration.Tests/EndToEndTests.cs
  fixtures/SampleRepo/                         # small buildable repo with seeded defects (its own .csproj + Program.cs etc.)
spikes/
  msbuildworkspace_probe/                      # Spike A console probe
  bailian_structured_probe/                    # Spike B console probe
  RESULTS.md                                   # spike findings + decisions
turtle/rubric_v1.md                            # frozen review rubric (data)
AGENTS.md  NOTES.md  README.md
```

## Task Index

1. Solution & project scaffolding (build + empty test green)
2. Spike A — MSBuildWorkspace vs Buildalyzer on .NET 10 (decision recorded)
3. Spike B — Bailian/Qwen OpenAI-compatible structured output (decision recorded)
4. Fixture repo `SampleRepo` with seeded defects
5. Core domain records + JSON serialization
6. Llm error taxonomy + `ErrorClassifier` (pure)
7. Llm config binding + `IChatClientFactory` + `MockChatClient`
8. Llm gateway: ordered fallback + Polly + `IStructuredChatClient` (degrade path)
9. Gatekeeper `DiffProvider` (LibGit2Sharp)
10. Gatekeeper `CompilationLoader` (+ `CompilationException`)
11. Gatekeeper `TurtleSyntaxWalker` + `SemanticAnalyzer`
12. Gatekeeper `PayloadGenerator`
13. Council `RubricLoader` + `PromptBuilder`
14. Council `PersonaRunner` (parallel) + `Arbiter`
15. Council `TurtleShellGuard` (+ audit)
16. Cli config + `ArtifactWriter` + `turtle review` command + `ReviewPipeline`
17. End-to-end offline integration test + project docs

### Task 1: Solution & project scaffolding

**Files:**
- Create: `CodeTurtleEngine.sln`, `Directory.Build.props`, `global.json`
- Create: `src/CodeTurtleEngine.Core/CodeTurtleEngine.Core.csproj` (+ 4 more src projects)
- Create: `tests/*/*.csproj` (5 unit-test projects + 1 integration project)

**Interfaces:**
- Consumes: nothing.
- Produces: a solution where `dotnet build` and `dotnet test` succeed with the projects referenced per the dependency flow `Cli -> {Council, Gatekeeper, Llm} -> Core`, `Council -> Gatekeeper`.

- [ ] **Step 1: Verify the .NET 10 SDK is installed**

Run: `dotnet --list-sdks`
Expected: at least one `10.0.x` SDK listed. If absent, install the .NET 10 SDK before continuing.

- [ ] **Step 2: Pin the SDK band and shared build props**

Create `global.json`:
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```
Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>CodeTurtleEngine</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create projects and wire references**

Run from repo root:
```bash
dotnet new sln -n CodeTurtleEngine
for p in Core Llm Gatekeeper Council Cli; do dotnet new classlib -n CodeTurtleEngine.$p -o src/CodeTurtleEngine.$p -f net10.0; done
dotnet new console -n CodeTurtleEngine.Cli -o src/CodeTurtleEngine.Cli -f net10.0   # overwrite Program.cs as console
for t in Core Llm Gatekeeper Council Cli; do dotnet new xunit -n CodeTurtleEngine.$t.Tests -o tests/CodeTurtleEngine.$t.Tests -f net10.0; done
dotnet new xunit -n CodeTurtleEngine.Integration.Tests -o tests/CodeTurtleEngine.Integration.Tests -f net10.0
```
Delete the template `Class1.cs` from each src project. Add all projects to the solution:
```bash
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName)
```
(On bash use: `dotnet sln add $(find src tests -name '*.csproj')`.)

Wire project references:
```bash
dotnet add src/CodeTurtleEngine.Llm reference src/CodeTurtleEngine.Core
dotnet add src/CodeTurtleEngine.Gatekeeper reference src/CodeTurtleEngine.Core
dotnet add src/CodeTurtleEngine.Council reference src/CodeTurtleEngine.Core src/CodeTurtleEngine.Gatekeeper src/CodeTurtleEngine.Llm
dotnet add src/CodeTurtleEngine.Cli reference src/CodeTurtleEngine.Core src/CodeTurtleEngine.Gatekeeper src/CodeTurtleEngine.Llm src/CodeTurtleEngine.Council
```
(`dotnet add <proj> reference` takes one target at a time if the multi-arg form is unsupported; repeat per target.)

Point each test project at its subject project, e.g.:
```bash
dotnet add tests/CodeTurtleEngine.Core.Tests reference src/CodeTurtleEngine.Core
dotnet add tests/CodeTurtleEngine.Llm.Tests reference src/CodeTurtleEngine.Llm
dotnet add tests/CodeTurtleEngine.Gatekeeper.Tests reference src/CodeTurtleEngine.Gatekeeper
dotnet add tests/CodeTurtleEngine.Council.Tests reference src/CodeTurtleEngine.Council
dotnet add tests/CodeTurtleEngine.Cli.Tests reference src/CodeTurtleEngine.Cli
dotnet add tests/CodeTurtleEngine.Integration.Tests reference src/CodeTurtleEngine.Cli
```

- [ ] **Step 4: Add a trivial passing test to prove the harness**

Create `tests/CodeTurtleEngine.Core.Tests/SmokeTests.cs`:
```csharp
namespace CodeTurtleEngine.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Harness_Runs() => Assert.True(true);
}
```

- [ ] **Step 5: Build and test**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds with zero warnings-as-errors; the smoke test passes.

- [ ] **Step 6: Commit**

```bash
git add CodeTurtleEngine.sln Directory.Build.props global.json src tests
git commit -m "chore: scaffold .NET 10 solution and project references"
```

---

### Task 2: Spike A — Roslyn compilation loader on .NET 10

**Files:**
- Create: `spikes/msbuildworkspace_probe/msbuildworkspace_probe.csproj`, `spikes/msbuildworkspace_probe/Program.cs`
- Create/Modify: `spikes/RESULTS.md`

**Interfaces:**
- Consumes: the `SampleRepo` fixture is NOT required yet; probe any small buildable repo (you may temporarily use `src/CodeTurtleEngine.Core` as the target).
- Produces: a recorded decision — use `MSBuildWorkspace` or `Buildalyzer` for `CompilationLoader` (Task 10).

- [ ] **Step 1: Create the probe project**

```bash
dotnet new console -n msbuildworkspace_probe -o spikes/msbuildworkspace_probe -f net10.0
dotnet add spikes/msbuildworkspace_probe package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add spikes/msbuildworkspace_probe package Microsoft.CodeAnalysis.Workspaces.MSBuild
```

- [ ] **Step 2: Write the probe**

`spikes/msbuildworkspace_probe/Program.cs`:
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics;

if (args.Length < 1) { Console.Error.WriteLine("usage: probe <path-to-csproj>"); return 2; }
var sw = Stopwatch.StartNew();
try
{
    using var workspace = MSBuildWorkspace.Create();
    workspace.WorkspaceFailed += (_, e) => Console.Error.WriteLine($"[workspace-failed] {e.Diagnostic.Message}");
    var project = await workspace.OpenProjectAsync(args[0]);
    var compilation = await project.GetCompilationAsync();
    sw.Stop();
    if (compilation is null) { Console.Error.WriteLine("compilation null"); return 1; }
    var symbols = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).Count();
    Console.WriteLine($"OK ms={sw.ElapsedMilliseconds} trees={compilation.SyntaxTreeCount()} typeSymbols={symbols}");
    Console.WriteLine($"Diagnostics/errors={compilation.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error)}");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine($"FAIL {ex.GetType().Name}: {ex.Message}"); return 1; }
```

- [ ] **Step 3: Run the probe against a real project**

Run: `dotnet run --project spikes/msbuildworkspace_probe -- src/CodeTurtleEngine.Core/CodeTurtleEngine.Core.csproj`
Expected: prints `OK ms=... trees=... typeSymbols=...`. Record wall-clock ms and whether `WorkspaceFailed` diagnostics appeared.

- [ ] **Step 4: If MSBuildWorkspace fails, probe Buildalyzer fallback**

```bash
dotnet add spikes/msbuildworkspace_probe package Buildalyzer
```
Add a second branch that uses `AnalyzerManager`/`ProjectAnalyzer` to run a build and obtain a Roslyn `Compilation` via `GetRoslynWorkspace().OpenProject(...).GetCompilationAsync()`. Run and record.

- [ ] **Step 5: Record the decision**

Append to `spikes/RESULTS.md`:
```markdown
## Spike A — Roslyn compilation loader (.NET 10), <date>
- Target probed: <csproj>
- MSBuildWorkspace: <OK/FAIL>, load ms=<n>, typeSymbols=<n>, workspace-failed diagnostics=<none/list>
- Buildalyzer (if tried): <OK/FAIL>, load ms=<n>
- Decision: CompilationLoader (Task 10) uses <MSBuildWorkspace | Buildalyzer> because <reason>.
```

- [ ] **Step 6: Commit**

```bash
git add spikes/msbuildworkspace_probe spikes/RESULTS.md
git commit -m "chore: spike A — Roslyn compilation loader on .NET 10"
```

---

### Task 3: Spike B — Bailian/Qwen structured output

**Files:**
- Create: `spikes/bailian_structured_probe/bailian_structured_probe.csproj`, `.../Program.cs`
- Modify: `spikes/RESULTS.md`

**Interfaces:**
- Consumes: env `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY` (Bailian OpenAI-compatible endpoint).
- Produces: a recorded decision — whether `StructuredChat` (Task 8) can rely on JSON-schema response format or must use the degrade path (explicit-JSON prompt + local validation).

- [ ] **Step 1: Create the probe project**

```bash
dotnet new console -n bailian_structured_probe -o spikes/bailian_structured_probe -f net10.0
dotnet add spikes/bailian_structured_probe package Microsoft.Extensions.AI.OpenAI
```

- [ ] **Step 2: Write the probe (schema mode vs degrade)**

`spikes/bailian_structured_probe/Program.cs`:
```csharp
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;

var baseUrl = Environment.GetEnvironmentVariable("TURTLE_LLM_BASE_URL")!;
var apiKey  = Environment.GetEnvironmentVariable("TURTLE_LLM_API_KEY")!;
var model   = "qwen3.8-flash";

IChatClient client = new OpenAIChatClient(
    new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) }),
    model);

var schema = """
{ "type":"object", "additionalProperties":false,
  "properties": { "severity": { "type":"string", "enum":["Info","Warning","Error"] },
                  "title": { "type":"string" } },
  "required": ["severity","title"] }
""";
var messages = new List<ChatMessage> {
    new(ChatRole.User, "Return JSON only: a code review finding with severity and title about an unawaited Task.")
};
try
{
    var opts = new ChatOptions { ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
        jsonSchemaFormatName: "finding", jsonSchema: BinaryData.FromString(schema)) };
    var resp = await client.GetResponseAsync(messages, opts);
    Console.WriteLine("SCHEMA-MODE-OUTPUT: " + resp.Text);
}
catch (Exception ex) { Console.WriteLine($"SCHEMA-MODE-UNSUPPORTED: {ex.GetType().Name}: {ex.Message}"); }

// Degrade path: plain prompt asking for JSON, then validate locally.
var degrade = new List<ChatMessage> {
    new(ChatRole.User, $"Respond with ONLY a JSON object matching this schema, no prose:\n{schema}\nTopic: an unawaited Task.")
};
var resp2 = await client.GetResponseAsync(degrade);
Console.WriteLine("DEGRADE-OUTPUT: " + resp2.Text);
```

- [ ] **Step 3: Run the probe (requires live creds)**

Run: `TURTLE_LIVE=1 dotnet run --project spikes/bailian_structured_probe`
Expected: prints `SCHEMA-MODE-OUTPUT:` with valid JSON, or `SCHEMA-MODE-UNSUPPORTED:` followed by a valid `DEGRADE-OUTPUT:`. Confirm at least one path yields schema-valid JSON.

- [ ] **Step 4: Record the decision**

Append to `spikes/RESULTS.md`:
```markdown
## Spike B — Bailian/Qwen structured output, <date>
- Endpoint: <base_url host>, model: qwen3.8-flash
- Schema response_format: <SUPPORTED | UNSUPPORTED (error)>
- Degrade (explicit-JSON prompt): <valid JSON? yes/no>
- Decision: StructuredChat (Task 8) primary path = <schema | degrade>; always run local System.Text.Json validation.
```

- [ ] **Step 5: Commit**

```bash
git add spikes/bailian_structured_probe spikes/RESULTS.md
git commit -m "chore: spike B — Bailian/Qwen structured output support"
```

---

### Task 4: Fixture repo `SampleRepo` with seeded defects

The fixture is a standalone buildable C# repo (NOT added to the solution) with known defects the gatekeeper must detect. It must build with warnings allowed (the unawaited task intentionally triggers CS4014), so it opts out of the root `Directory.Build.props`.

**Files:**
- Create: `tests/fixtures/Directory.Build.props` (empty, stops upward MSBuild prop inheritance)
- Create: `tests/fixtures/SampleRepo/SampleRepo.csproj`
- Create: `tests/fixtures/SampleRepo/{IPaymentGateway,ILogger,PaymentService,DataAccess,DiRegistration}.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a fixture at `tests/fixtures/SampleRepo/SampleRepo.csproj` containing type `SampleRepo.PaymentService.ProcessPaymentAsync` (missing `ConfigureAwait(false)`, implicit boxing, LINQ closure allocation, unawaited task), `SampleRepo.DataAccess.BuildQuery` (SQL string concatenation), and `SampleRepo.SingletonOrchestrator` (constructor-injected collaborator). Later tasks resolve these symbols by these exact FQNs.

- [ ] **Step 1: Stop prop inheritance for fixtures**

`tests/fixtures/Directory.Build.props`:
```xml
<Project />
```

- [ ] **Step 2: Create the fixture project**

`tests/fixtures/SampleRepo/SampleRepo.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);CS4014</NoWarn>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Add interfaces**

`tests/fixtures/SampleRepo/IPaymentGateway.cs`:
```csharp
namespace SampleRepo;

public interface IPaymentGateway
{
    Task<bool> ChargeAsync(int amountCents);
}
```
`tests/fixtures/SampleRepo/ILogger.cs`:
```csharp
namespace SampleRepo;

public interface ILogger
{
    void Log(string message);
}
```

- [ ] **Step 4: Add the defect-bearing service**

`tests/fixtures/SampleRepo/PaymentService.cs`:
```csharp
namespace SampleRepo;

public class PaymentService
{
    private readonly IPaymentGateway _gateway;
    private readonly ILogger _logger;

    public PaymentService(IPaymentGateway gateway, ILogger logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<decimal> ProcessPaymentAsync(IReadOnlyList<int> amounts)
    {
        // Defect: missing ConfigureAwait(false).
        bool ok = await _gateway.ChargeAsync(amounts.Sum());

        // Defect: LINQ closure allocation (captured 'threshold').
        int threshold = 100;
        var large = amounts.Where(a => a > threshold).ToList();

        // Defect: implicit boxing of a value type.
        object boxed = large.Count;
        _logger.Log("processed " + boxed.ToString());

        // Defect: unawaited task (fire-and-forget).
        NotifyAsync();

        return ok ? large.Count : 0m;
    }

    private Task NotifyAsync() => Task.CompletedTask;
}
```

- [ ] **Step 5: Add the SQL-injection and DI fixtures**

`tests/fixtures/SampleRepo/DataAccess.cs`:
```csharp
namespace SampleRepo;

public class DataAccess
{
    // Defect: raw string concatenation builds a WHERE clause (SQL injection vector).
    public string BuildQuery(string userName)
    {
        return "SELECT * FROM Users WHERE Name = '" + userName + "'";
    }
}
```
`tests/fixtures/SampleRepo/DiRegistration.cs`:
```csharp
namespace SampleRepo;

public interface IEngine
{
    void Run();
}

public class TransientWorker : IEngine
{
    public void Run() { }
}

// Constructor-injected collaborator captured in a field (Idiomatic Architect judges lifetime).
public class SingletonOrchestrator
{
    private readonly TransientWorker _worker;

    public SingletonOrchestrator(TransientWorker worker)
    {
        _worker = worker;
    }

    public void RunAll() => _worker.Run();
}
```

- [ ] **Step 6: Verify the fixture builds**

Run: `dotnet build tests/fixtures/SampleRepo/SampleRepo.csproj`
Expected: build succeeds (CS4014 suppressed). This build is the task's test cycle.

- [ ] **Step 7: Commit**

```bash
git add tests/fixtures
git commit -m "test: add SampleRepo fixture with seeded review defects"
```

---

### Task 5: Core domain records + JSON serialization

**Files:**
- Create: `src/CodeTurtleEngine.Core/Domain.cs`, `Payload.cs`, `Findings.cs`, `TurtleJson.cs`
- Test: `tests/CodeTurtleEngine.Core.Tests/PayloadJsonTests.cs` (replace `SmokeTests.cs`)

**Interfaces:**
- Consumes: nothing.
- Produces (exact shapes later tasks rely on):
  - `enum Severity { Info, Nit, Warning, Error, Critical }`
  - `enum PersonaRole { AllocationsPerformance, SecurityAuditor, IdiomaticArchitect, Arbiter }`
  - `enum GuardMode { Strip, Flag }`
  - `record MethodFacts(string Method, IReadOnlyList<string> Allocations, string? AsyncHealth, IReadOnlyList<string> Dependencies, IReadOnlyList<string> ResolvedSymbols)`
  - `record FileFacts(string FilePath, IReadOnlyList<MethodFacts> Methods)`
  - `record RoslynPayload(string RepoSlug, string DiffBaseline, IReadOnlyList<FileFacts> Files)`
  - `record ReviewFinding(PersonaRole Persona, Severity Severity, string Title, string Detail, string Location, IReadOnlyList<string> CitedSymbolFqns)`
  - `record PersonaVerdict(PersonaRole Persona, IReadOnlyList<ReviewFinding> Findings)`
  - `record GuardResult(string CitedFqn, bool Verified)`
  - `record CouncilVerdict(IReadOnlyList<PersonaVerdict> Personas, IReadOnlyList<ReviewFinding> Synthesized, IReadOnlyList<GuardResult> GuardAudit)`
  - `static class TurtleJson { public static JsonSerializerOptions Options { get; } }` — PascalCase preserved, enums as strings, nulls ignored.

- [ ] **Step 1: Write the failing test**

`tests/CodeTurtleEngine.Core.Tests/PayloadJsonTests.cs`:
```csharp
using System.Text.Json;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Core.Tests;

public class PayloadJsonTests
{
    [Fact]
    public void RoslynPayload_RoundTrips_Fields()
    {
        var payload = new RoslynPayload("SampleRepo", "HEAD~1..HEAD", new[]
        {
            new FileFacts("PaymentService.cs", new[]
            {
                new MethodFacts("ProcessPaymentAsync",
                    new[] { "LINQ Closure (Line 18)", "Implicit Boxing (Line 21)" },
                    "Missing ConfigureAwait(false) on IPaymentGateway invocation",
                    new[] { "IPaymentGateway", "ILogger" },
                    new[] { "SampleRepo.IPaymentGateway", "SampleRepo.ILogger" })
            })
        });

        var json = JsonSerializer.Serialize(payload, TurtleJson.Options);
        var back = JsonSerializer.Deserialize<RoslynPayload>(json, TurtleJson.Options)!;

        Assert.Equal("ProcessPaymentAsync", back.Files[0].Methods[0].Method);
        Assert.Equal(2, back.Files[0].Methods[0].Allocations.Count);
        Assert.Equal("SampleRepo.IPaymentGateway", back.Files[0].Methods[0].ResolvedSymbols[0]);
        Assert.Contains("\"Method\":\"ProcessPaymentAsync\"", json);
    }

    [Fact]
    public void Enums_Serialize_As_Strings()
    {
        var f = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection",
            "Raw concatenation builds a WHERE clause.", "DataAccess.cs:11",
            new[] { "SampleRepo.DataAccess.BuildQuery" });

        var json = JsonSerializer.Serialize(f, TurtleJson.Options);

        Assert.Contains("\"Persona\":\"SecurityAuditor\"", json);
        Assert.Contains("\"Severity\":\"Error\"", json);
    }
}
```
Delete `tests/CodeTurtleEngine.Core.Tests/SmokeTests.cs`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Core.Tests --filter "FullyQualifiedName~PayloadJsonTests"`
Expected: FAIL — `CodeTurtleEngine.Core.RoslynPayload` / `TurtleJson` do not exist (compile error).

- [ ] **Step 3: Write the domain records**

`src/CodeTurtleEngine.Core/Domain.cs`:
```csharp
namespace CodeTurtleEngine.Core;

public enum Severity { Info, Nit, Warning, Error, Critical }

public enum PersonaRole { AllocationsPerformance, SecurityAuditor, IdiomaticArchitect, Arbiter }

public enum GuardMode { Strip, Flag }
```
`src/CodeTurtleEngine.Core/Payload.cs`:
```csharp
namespace CodeTurtleEngine.Core;

public sealed record MethodFacts(
    string Method,
    IReadOnlyList<string> Allocations,
    string? AsyncHealth,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> ResolvedSymbols);

public sealed record FileFacts(
    string FilePath,
    IReadOnlyList<MethodFacts> Methods);

public sealed record RoslynPayload(
    string RepoSlug,
    string DiffBaseline,
    IReadOnlyList<FileFacts> Files);
```
`src/CodeTurtleEngine.Core/Findings.cs`:
```csharp
namespace CodeTurtleEngine.Core;

public sealed record ReviewFinding(
    PersonaRole Persona,
    Severity Severity,
    string Title,
    string Detail,
    string Location,
    IReadOnlyList<string> CitedSymbolFqns);

public sealed record PersonaVerdict(
    PersonaRole Persona,
    IReadOnlyList<ReviewFinding> Findings);

public sealed record GuardResult(
    string CitedFqn,
    bool Verified);

public sealed record CouncilVerdict(
    IReadOnlyList<PersonaVerdict> Personas,
    IReadOnlyList<ReviewFinding> Synthesized,
    IReadOnlyList<GuardResult> GuardAudit);
```
`src/CodeTurtleEngine.Core/TurtleJson.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeTurtleEngine.Core;

public static class TurtleJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Core.Tests --filter "FullyQualifiedName~PayloadJsonTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/CodeTurtleEngine.Core tests/CodeTurtleEngine.Core.Tests
git commit -m "feat: add Core domain records and JSON serialization"
```

---

### Task 6: Llm error taxonomy + `ErrorClassifier`

**Files:**
- Create: `src/CodeTurtleEngine.Llm/ErrorClassifier.cs`, `src/CodeTurtleEngine.Llm/LlmExceptions.cs`
- Test: `tests/CodeTurtleEngine.Llm.Tests/ErrorClassifierTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum LlmErrorKind { None, Auth, RateLimit, ServerError, Timeout, BadOutput, Unknown }`
  - `static LlmErrorKind ErrorClassifier.ClassifyStatus(int httpStatus, string? errorBody = null)`
  - `class ProviderException(string message, IReadOnlyList<string> attemptLog, Exception? inner = null)` with `IReadOnlyList<string> AttemptLog`
  - `class ModelException(string message, Exception? inner = null)`

- [ ] **Step 1: Add package**

Run: `dotnet add src/CodeTurtleEngine.Llm package Microsoft.Extensions.AI.OpenAI`
(this also brings `Microsoft.Extensions.AI` abstractions.)

- [ ] **Step 2: Write the failing test**

`tests/CodeTurtleEngine.Llm.Tests/ErrorClassifierTests.cs`:
```csharp
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Llm.Tests;

public class ErrorClassifierTests
{
    [Theory]
    [InlineData(401, null, LlmErrorKind.Auth)]
    [InlineData(403, null, LlmErrorKind.Auth)]
    [InlineData(429, null, LlmErrorKind.RateLimit)]
    [InlineData(500, null, LlmErrorKind.ServerError)]
    [InlineData(503, null, LlmErrorKind.ServerError)]
    [InlineData(408, null, LlmErrorKind.Timeout)]
    [InlineData(200, null, LlmErrorKind.None)]
    [InlineData(200, "model overloaded", LlmErrorKind.BadOutput)]
    [InlineData(418, null, LlmErrorKind.Unknown)]
    public void Classifies(int status, string? body, LlmErrorKind expected)
        => Assert.Equal(expected, ErrorClassifier.ClassifyStatus(status, body));
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~ErrorClassifierTests"`
Expected: FAIL — `ErrorClassifier` does not exist.

- [ ] **Step 4: Implement**

`src/CodeTurtleEngine.Llm/ErrorClassifier.cs`:
```csharp
namespace CodeTurtleEngine.Llm;

public enum LlmErrorKind { None, Auth, RateLimit, ServerError, Timeout, BadOutput, Unknown }

public static class ErrorClassifier
{
    public static LlmErrorKind ClassifyStatus(int httpStatus, string? errorBody = null)
    {
        if (httpStatus is 401 or 403) return LlmErrorKind.Auth;
        if (httpStatus == 429) return LlmErrorKind.RateLimit;
        if (httpStatus == 408) return LlmErrorKind.Timeout;
        if (httpStatus >= 500 && httpStatus <= 599) return LlmErrorKind.ServerError;
        if (httpStatus >= 200 && httpStatus < 300)
            return string.IsNullOrWhiteSpace(errorBody) ? LlmErrorKind.None : LlmErrorKind.BadOutput;
        return LlmErrorKind.Unknown;
    }
}
```
`src/CodeTurtleEngine.Llm/LlmExceptions.cs`:
```csharp
namespace CodeTurtleEngine.Llm;

public sealed class ProviderException : Exception
{
    public IReadOnlyList<string> AttemptLog { get; }

    public ProviderException(string message, IReadOnlyList<string> attemptLog, Exception? inner = null)
        : base(message, inner) => AttemptLog = attemptLog;
}

public sealed class ModelException : Exception
{
    public ModelException(string message, Exception? inner = null) : base(message, inner) { }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~ErrorClassifierTests"`
Expected: PASS (9 cases).

- [ ] **Step 6: Commit**

```bash
git add src/CodeTurtleEngine.Llm/ErrorClassifier.cs src/CodeTurtleEngine.Llm/LlmExceptions.cs tests/CodeTurtleEngine.Llm.Tests/ErrorClassifierTests.cs
git commit -m "feat: add Llm error taxonomy and pure ErrorClassifier"
```

---

### Task 7: Llm config + `IChatClientFactory` + `MockChatClient`

**Files:**
- Create: `src/CodeTurtleEngine.Llm/LlmOptions.cs`, `ChatClientFactory.cs`, `MockChatClient.cs`
- Test: `tests/CodeTurtleEngine.Llm.Tests/ChatClientFactoryTests.cs`

**Interfaces:**
- Consumes: `ProviderException` (Task 6); `IChatClient`, `ChatMessage`, `ChatOptions`, `ChatResponse` from `Microsoft.Extensions.AI`.
- Produces:
  - `class RouteOptions { string Name; string BaseUrlEnv; string ApiKeyEnv; string Model }` (settable props)
  - `class LlmOptions { List<RouteOptions> Routes; Dictionary<string,string> ModelRoles }` (const `SectionName = "Llm"`)
  - `interface IChatClientFactory { IChatClient Create(RouteOptions route, string model) }`
  - `class BailianChatClientFactory : IChatClientFactory`
  - `class MockChatClient : IChatClient` with `MockChatClient(string fixedJson)`, `MockChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, string> responder)`, and `int CallCount { get; }`

> **API note:** This plan targets the current `Microsoft.Extensions.AI` surface (`IChatClient.GetResponseAsync(...) -> ChatResponse`, `ChatResponse.Text`). If the referenced package version still uses the older `CompleteAsync(...) -> ChatCompletion` surface, rename the method/return accordingly in `MockChatClient` and downstream callers; the spike in Task 3 already exercises the target surface.

- [ ] **Step 1: Write the failing test**

`tests/CodeTurtleEngine.Llm.Tests/ChatClientFactoryTests.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

public class ChatClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", null);
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", null);
    }

    private static RouteOptions Route() => new()
    {
        Name = "r",
        BaseUrlEnv = "TURTLE_TEST_BASE",
        ApiKeyEnv = "TURTLE_TEST_KEY",
        Model = "qwen3.8-flash"
    };

    [Fact]
    public void Throws_ProviderException_When_BaseUrl_Env_Missing()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        var ex = Assert.Throws<ProviderException>(() => new BailianChatClientFactory().Create(Route(), "qwen3.8-flash"));
        Assert.Contains("TURTLE_TEST_BASE", ex.Message);
    }

    [Fact]
    public void Builds_Client_When_Env_Set()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", "https://localhost/v1");
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        IChatClient client = new BailianChatClientFactory().Create(Route(), "qwen3.8-flash");
        Assert.NotNull(client);
    }

    [Fact]
    public async Task MockChatClient_Returns_Canned_And_Counts()
    {
        var mock = new MockChatClient("{\"ok\":true}");
        var resp = await mock.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });
        Assert.Equal("{\"ok\":true}", resp.Text);
        Assert.Equal(1, mock.CallCount);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~ChatClientFactoryTests"`
Expected: FAIL — `BailianChatClientFactory` / `MockChatClient` do not exist.

- [ ] **Step 3: Implement options + factory + mock**

`src/CodeTurtleEngine.Llm/LlmOptions.cs`:
```csharp
namespace CodeTurtleEngine.Llm;

public sealed class RouteOptions
{
    public string Name { get; set; } = "";
    public string BaseUrlEnv { get; set; } = "";
    public string ApiKeyEnv { get; set; } = "";
    public string Model { get; set; } = "";
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public List<RouteOptions> Routes { get; set; } = new();
    public Dictionary<string, string> ModelRoles { get; set; } = new();
}
```
`src/CodeTurtleEngine.Llm/ChatClientFactory.cs`:
```csharp
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace CodeTurtleEngine.Llm;

public interface IChatClientFactory
{
    IChatClient Create(RouteOptions route, string model);
}

public sealed class BailianChatClientFactory : IChatClientFactory
{
    public IChatClient Create(RouteOptions route, string model)
    {
        var baseUrl = Environment.GetEnvironmentVariable(route.BaseUrlEnv)
            ?? throw new ProviderException($"Missing env '{route.BaseUrlEnv}'.", Array.Empty<string>());
        var apiKey = Environment.GetEnvironmentVariable(route.ApiKeyEnv)
            ?? throw new ProviderException($"Missing env '{route.ApiKeyEnv}'.", Array.Empty<string>());

        var openAi = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        return new OpenAIChatClient(openAi, model);
    }
}
```
`src/CodeTurtleEngine.Llm/MockChatClient.cs`:
```csharp
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CodeTurtleEngine.Llm;

public sealed class MockChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, string> _responder;

    public int CallCount { get; private set; }

    public MockChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, string> responder)
        => _responder = responder;

    public MockChatClient(string fixedJson) : this((_, _) => fixedJson) { }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        var text = _responder(messages, options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(IChatClient) ? this : null;

    public void Dispose() { }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~ChatClientFactoryTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/CodeTurtleEngine.Llm/LlmOptions.cs src/CodeTurtleEngine.Llm/ChatClientFactory.cs src/CodeTurtleEngine.Llm/MockChatClient.cs tests/CodeTurtleEngine.Llm.Tests/ChatClientFactoryTests.cs
git commit -m "feat: add Bailian chat client factory and mock client"
```

---

### Task 8: Llm gateway — ordered fallback + Polly + `IStructuredChatClient`

**Files:**
- Create: `src/CodeTurtleEngine.Llm/LlmGateway.cs`, `src/CodeTurtleEngine.Llm/StructuredChat.cs`
- Test: `tests/CodeTurtleEngine.Llm.Tests/TestDoubles.cs`, `LlmGatewayTests.cs`, `StructuredChatTests.cs`

**Interfaces:**
- Consumes: `IChatClientFactory`, `RouteOptions`, `LlmOptions`, `MockChatClient` (Task 7); `ProviderException`, `ModelException` (Task 6); `TurtleJson` (Task 5).
- Produces:
  - `interface ILlmGateway { Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) }`
  - `class LlmGateway(LlmOptions options, IChatClientFactory factory, ResiliencePipeline? pipeline = null) : ILlmGateway`
  - `interface IStructuredChatClient { Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages, string jsonSchema, CancellationToken ct = default) }`
  - `class StructuredChatClient(ILlmGateway gateway) : IStructuredChatClient`
  - `static string StructuredChatClient.ExtractJson(string raw)` (internal, exposed for tests via `InternalsVisibleTo` or made public)

- [ ] **Step 1: Add resilience package**

Run: `dotnet add src/CodeTurtleEngine.Llm package Polly`

- [ ] **Step 2: Write the test doubles**

`tests/CodeTurtleEngine.Llm.Tests/TestDoubles.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

internal sealed class FakeFactory : IChatClientFactory
{
    private readonly Func<RouteOptions, string, IChatClient> _make;
    public FakeFactory(Func<RouteOptions, string, IChatClient> make) => _make = make;
    public IChatClient Create(RouteOptions route, string model) => _make(route, model);
}

internal sealed class FakeGateway : ILlmGateway
{
    private readonly Func<string, IReadOnlyList<ChatMessage>, ChatOptions?, Task<string>> _fn;
    public FakeGateway(Func<string, IReadOnlyList<ChatMessage>, ChatOptions?, Task<string>> fn) => _fn = fn;
    public Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default) => _fn(role, messages, options);
}
```

- [ ] **Step 3: Write the failing gateway tests**

`tests/CodeTurtleEngine.Llm.Tests/LlmGatewayTests.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;
using Polly;

namespace CodeTurtleEngine.Llm.Tests;

public class LlmGatewayTests
{
    private static LlmOptions Opts() => new()
    {
        Routes =
        {
            new RouteOptions { Name = "primary", Model = "qwen3.8-flash" },
            new RouteOptions { Name = "backup", Model = "qwen3.8-flash" }
        },
        ModelRoles = { ["fast"] = "qwen3.8-flash" }
    };

    private static IReadOnlyList<ChatMessage> Msgs() => new[] { new ChatMessage(ChatRole.User, "hi") };

    [Fact]
    public async Task Falls_Back_To_Second_Route_When_First_Fails()
    {
        var factory = new FakeFactory((route, _) => route.Name == "primary"
            ? new MockChatClient((_, _) => throw new InvalidOperationException("primary down"))
            : new MockChatClient("{\"ok\":true}"));

        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.None);
        var result = await gw.CompleteAsync("fast", Msgs());

        Assert.Equal("{\"ok\":true}", result);
    }

    [Fact]
    public async Task Throws_ProviderException_When_All_Routes_Fail()
    {
        var factory = new FakeFactory((_, _) => new MockChatClient((_, _) => throw new InvalidOperationException("down")));
        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.None);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => gw.CompleteAsync("fast", Msgs()));
        Assert.Equal(2, ex.AttemptLog.Count);
    }

    [Fact]
    public async Task Throws_ModelException_For_Unknown_Role()
    {
        var factory = new FakeFactory((_, _) => new MockChatClient("x"));
        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.None);

        await Assert.ThrowsAsync<ModelException>(() => gw.CompleteAsync("nope", Msgs()));
    }
}
```

- [ ] **Step 4: Run gateway tests to verify they fail**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~LlmGatewayTests"`
Expected: FAIL — `LlmGateway` does not exist.

- [ ] **Step 5: Implement the gateway**

`src/CodeTurtleEngine.Llm/LlmGateway.cs`:
```csharp
using Microsoft.Extensions.AI;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace CodeTurtleEngine.Llm;

public interface ILlmGateway
{
    Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default);
}

public sealed class LlmGateway : ILlmGateway
{
    private readonly LlmOptions _options;
    private readonly IChatClientFactory _factory;
    private readonly ResiliencePipeline _pipeline;

    public LlmGateway(LlmOptions options, IChatClientFactory factory, ResiliencePipeline? pipeline = null)
    {
        _options = options;
        _factory = factory;
        _pipeline = pipeline ?? DefaultPipeline();
    }

    public async Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default)
    {
        if (!_options.ModelRoles.TryGetValue(role, out var model))
            throw new ModelException($"No model mapped for role '{role}'.");

        var attemptLog = new List<string>();
        Exception? last = null;

        foreach (var route in _options.Routes)
        {
            try
            {
                var client = _factory.Create(route, model);
                var resp = await _pipeline.ExecuteAsync(
                    token => client.GetResponseAsync(messages, options, token), ct);
                return resp.Text;
            }
            catch (Exception ex)
            {
                last = ex;
                attemptLog.Add($"{route.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        throw new ProviderException($"All routes failed for role '{role}'.", attemptLog, last);
    }

    private static ResiliencePipeline DefaultPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
}
```

- [ ] **Step 6: Run gateway tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~LlmGatewayTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Write the failing structured-chat tests**

`tests/CodeTurtleEngine.Llm.Tests/StructuredChatTests.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

public class StructuredChatTests
{
    private sealed record Dto(string Title, int Severity);

    private static IReadOnlyList<ChatMessage> Msgs() => new[] { new ChatMessage(ChatRole.User, "hi") };
    private static ILlmGateway Returning(string text) => new FakeGateway((_, _, _) => Task.FromResult(text));

    [Fact]
    public async Task Parses_Valid_Json()
    {
        var sc = new StructuredChatClient(Returning("{\"Title\":\"x\",\"Severity\":2}"));
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("x", dto.Title);
        Assert.Equal(2, dto.Severity);
    }

    [Fact]
    public async Task Extracts_Json_From_Prose()
    {
        var sc = new StructuredChatClient(Returning("Sure: {\"Title\":\"y\",\"Severity\":1} done"));
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("y", dto.Title);
    }

    [Fact]
    public async Task Throws_ModelException_On_Garbage()
    {
        var sc = new StructuredChatClient(Returning("not json at all"));
        await Assert.ThrowsAsync<ModelException>(() => sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}"));
    }

    [Fact]
    public async Task Degrades_When_Schema_Mode_Unsupported()
    {
        var gw = new FakeGateway((_, _, o) => o?.ResponseFormat is not null
            ? throw new InvalidOperationException("schema unsupported")
            : Task.FromResult("{\"Title\":\"z\",\"Severity\":3}"));
        var sc = new StructuredChatClient(gw);
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("z", dto.Title);
    }
}
```

- [ ] **Step 8: Run structured-chat tests to verify they fail**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests --filter "FullyQualifiedName~StructuredChatTests"`
Expected: FAIL — `StructuredChatClient` does not exist.

- [ ] **Step 9: Implement structured chat**

`src/CodeTurtleEngine.Llm/StructuredChat.cs`:
```csharp
using CodeTurtleEngine.Core;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeTurtleEngine.Llm;

public interface IStructuredChatClient
{
    Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default);
}

public sealed class StructuredChatClient : IStructuredChatClient
{
    private readonly ILlmGateway _gateway;

    public StructuredChatClient(ILlmGateway gateway) => _gateway = gateway;

    public async Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        try
        {
            var opts = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("schema", BinaryData.FromString(jsonSchema))
            };
            var raw = await _gateway.CompleteAsync(role, messages, opts, ct);
            return Parse<T>(raw);
        }
        catch (Exception ex) when (ex is not ModelException)
        {
            var degrade = new List<ChatMessage>(messages)
            {
                new(ChatRole.User, $"Respond with ONLY a JSON value matching this schema, no prose:\n{jsonSchema}")
            };
            var raw = await _gateway.CompleteAsync(role, degrade, null, ct);
            return Parse<T>(raw);
        }
    }

    private static T Parse<T>(string raw)
    {
        var text = ExtractJson(raw);
        try
        {
            return JsonSerializer.Deserialize<T>(text, TurtleJson.Options)
                ?? throw new ModelException("Deserialized to null.");
        }
        catch (JsonException jx)
        {
            throw new ModelException($"Invalid JSON from model: {jx.Message}", jx);
        }
    }

    public static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw.Trim();
    }
}
```

- [ ] **Step 10: Run structured-chat tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Llm.Tests`
Expected: PASS (all Llm tests: ErrorClassifier + Factory + Gateway + StructuredChat).

- [ ] **Step 11: Commit**

```bash
git add src/CodeTurtleEngine.Llm/LlmGateway.cs src/CodeTurtleEngine.Llm/StructuredChat.cs tests/CodeTurtleEngine.Llm.Tests/TestDoubles.cs tests/CodeTurtleEngine.Llm.Tests/LlmGatewayTests.cs tests/CodeTurtleEngine.Llm.Tests/StructuredChatTests.cs
git commit -m "feat: add Llm gateway with fallback, resilience, and structured output"
```

---

### Task 9: Gatekeeper `DiffProvider` (LibGit2Sharp)

**Files:**
- Create: `src/CodeTurtleEngine.Gatekeeper/DiffProvider.cs`, `src/CodeTurtleEngine.Gatekeeper/DiffException.cs`
- Test: `tests/CodeTurtleEngine.Gatekeeper.Tests/DiffProviderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `interface IDiffProvider { IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef) }`
  - `class GitDiffProvider : IDiffProvider` — `baselineRef == null` diffs working tree vs `HEAD`; otherwise diffs `baselineRef` (branch name or commit SHA) vs `HEAD`.
  - `class DiffException(string message, Exception? inner = null)`

> **API note:** LibGit2Sharp `Diff.Compare<TreeChanges>(...)` overloads and `Repository.Commit(Signature, Signature)` signatures vary slightly by version; adjust to the referenced version if the compiler flags an overload.

- [ ] **Step 1: Add package**

Run: `dotnet add src/CodeTurtleEngine.Gatekeeper package LibGit2Sharp`

- [ ] **Step 2: Write the failing test**

`tests/CodeTurtleEngine.Gatekeeper.Tests/DiffProviderTests.cs`:
```csharp
using CodeTurtleEngine.Gatekeeper;
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class DiffProviderTests
{
    [Fact]
    public void Returns_Changed_Cs_Files_WorkingTree_Vs_Head()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A {}\n");
                File.WriteAllText(Path.Combine(dir, "B.txt"), "ignore\n");
                Commands.Stage(repo, "A.cs");
                Commands.Stage(repo, "B.txt");
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                repo.Commit(sig, sig);

                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A { int x; }\n");

                var changed = new GitDiffProvider().GetChangedCSharpFiles(dir, null);

                Assert.Contains("A.cs", changed);
                Assert.DoesNotContain("B.txt", changed);
            }
        }
        finally
        {
            ClearAttributes(dir);
            Directory.Delete(dir, true);
        }
    }

    private static void ClearAttributes(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~DiffProviderTests"`
Expected: FAIL — `GitDiffProvider` does not exist.

- [ ] **Step 4: Implement**

`src/CodeTurtleEngine.Gatekeeper/DiffException.cs`:
```csharp
namespace CodeTurtleEngine.Gatekeeper;

public sealed class DiffException : Exception
{
    public DiffException(string message, Exception? inner = null) : base(message, inner) { }
}
```
`src/CodeTurtleEngine.Gatekeeper/DiffProvider.cs`:
```csharp
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper;

public interface IDiffProvider
{
    IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef);
}

public sealed class GitDiffProvider : IDiffProvider
{
    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef)
    {
        try
        {
            using var repo = new Repository(repoPath);
            TreeChanges changes = baselineRef is null
                ? repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory)
                : Compare(repo, baselineRef);

            return changes
                .Where(c => c.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Path)
                .Distinct()
                .ToList();
        }
        catch (Exception ex) when (ex is not DiffException)
        {
            throw new DiffException($"Diff failed for '{repoPath}': {ex.Message}", ex);
        }
    }

    private static TreeChanges Compare(Repository repo, string baselineRef)
    {
        var branch = repo.Branches[baselineRef];
        var baselineCommit = branch?.Tip ?? repo.Lookup<Commit>(baselineRef)
            ?? throw new DiffException($"Baseline '{baselineRef}' not found.");
        return repo.Diff.Compare<TreeChanges>(baselineCommit.Tree, repo.Head.Tip.Tree);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~DiffProviderTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CodeTurtleEngine.Gatekeeper/DiffProvider.cs src/CodeTurtleEngine.Gatekeeper/DiffException.cs tests/CodeTurtleEngine.Gatekeeper.Tests/DiffProviderTests.cs
git commit -m "feat: add LibGit2Sharp diff provider for changed C# files"
```

---

### Task 10: Gatekeeper `CompilationLoader`

**Files:**
- Create: `src/CodeTurtleEngine.Gatekeeper/CompilationLoader.cs`, `src/CodeTurtleEngine.Gatekeeper/CompilationException.cs`
- Test: `tests/CodeTurtleEngine.Gatekeeper.Tests/TestPaths.cs`, `CompilationLoaderTests.cs`

**Interfaces:**
- Consumes: Roslyn `Compilation`, `Project`; `MSBuildWorkspace` (or Buildalyzer per Spike A decision).
- Produces:
  - `interface ICompilationLoader { Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default) }`
  - `record LoadedCompilation(Microsoft.CodeAnalysis.Compilation Compilation, IDisposable Workspace)` — holds the workspace for the pipeline's lifetime so symbols resolve; disposed by the pipeline.
  - `class CompilationException(string message, Exception? inner = null)`

> Use the Spike A decision. Primary implementation below is `MSBuildWorkspace`; if Spike A chose Buildalyzer, swap the body but keep `ICompilationLoader`/`LoadedCompilation` identical.

- [ ] **Step 1: Add packages**

Run:
```bash
dotnet add src/CodeTurtleEngine.Gatekeeper package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add src/CodeTurtleEngine.Gatekeeper package Microsoft.CodeAnalysis.Workspaces.MSBuild
```

- [ ] **Step 2: Add the test path helper**

`tests/CodeTurtleEngine.Gatekeeper.Tests/TestPaths.cs`:
```csharp
namespace CodeTurtleEngine.Gatekeeper.Tests;

public static class TestPaths
{
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodeTurtleEngine.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repo root (sln) not found.");
        }
    }

    public static string SampleRepoCsproj =>
        Path.Combine(RepoRoot, "tests", "fixtures", "SampleRepo", "SampleRepo.csproj");
}
```

- [ ] **Step 3: Write the failing test**

`tests/CodeTurtleEngine.Gatekeeper.Tests/CompilationLoaderTests.cs`:
```csharp
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class CompilationLoaderTests
{
    [Fact]
    public async Task Loads_SampleRepo_And_Resolves_PaymentService()
    {
        var loader = new MsBuildCompilationLoader();
        var loaded = await loader.LoadAsync(TestPaths.SampleRepoCsproj);

        var sym = loaded.Compilation.GetTypeByMetadataName("SampleRepo.PaymentService");
        Assert.NotNull(sym);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~CompilationLoaderTests"`
Expected: FAIL — `MsBuildCompilationLoader` does not exist.

- [ ] **Step 5: Implement**

`src/CodeTurtleEngine.Gatekeeper/CompilationException.cs`:
```csharp
namespace CodeTurtleEngine.Gatekeeper;

public sealed class CompilationException : Exception
{
    public CompilationException(string message, Exception? inner = null) : base(message, inner) { }
}
```
`src/CodeTurtleEngine.Gatekeeper/CompilationLoader.cs`:
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record LoadedCompilation(Compilation Compilation, IDisposable Workspace);

public interface ICompilationLoader
{
    Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default);
}

public sealed class MsBuildCompilationLoader : ICompilationLoader
{
    public async Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
    {
        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        try
        {
            Project project = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                ? (await workspace.OpenSolutionAsync(path, cancellationToken: ct)).Projects.FirstOrDefault()
                    ?? throw new CompilationException($"No projects in solution '{path}'.")
                : await workspace.OpenProjectAsync(path, cancellationToken: ct);

            var compilation = await project.GetCompilationAsync(ct)
                ?? throw new CompilationException($"Compilation was null for '{path}'.");

            return new LoadedCompilation(compilation, workspace);
        }
        catch (Exception ex) when (ex is not CompilationException)
        {
            workspace.Dispose();
            throw new CompilationException($"Failed to load compilation from '{path}': {ex.Message}", ex);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~CompilationLoaderTests"`
Expected: PASS. (First run restores/builds the fixture; allow time.)

- [ ] **Step 7: Commit**

```bash
git add src/CodeTurtleEngine.Gatekeeper/CompilationLoader.cs src/CodeTurtleEngine.Gatekeeper/CompilationException.cs tests/CodeTurtleEngine.Gatekeeper.Tests/TestPaths.cs tests/CodeTurtleEngine.Gatekeeper.Tests/CompilationLoaderTests.cs
git commit -m "feat: add MSBuildWorkspace compilation loader"
```

---

### Task 11: Gatekeeper `TurtleSyntaxWalker` + `SemanticAnalyzer`

Extracts per-method semantic facts from a compiled tree: constructor dependencies, resolved type symbols, async health (missing `ConfigureAwait(false)`), and allocations (implicit boxing, LINQ closures). Deterministic only — lifetime/captive-dependency *judgment* is left to the council; the gatekeeper only records the injected-dependency facts.

**Files:**
- Create: `src/CodeTurtleEngine.Gatekeeper/TurtleSyntaxWalker.cs`, `src/CodeTurtleEngine.Gatekeeper/SemanticAnalyzer.cs`
- Test: `tests/CodeTurtleEngine.Gatekeeper.Tests/SampleRepoCompilationFixture.cs`, `SemanticAnalyzerTests.cs`

**Interfaces:**
- Consumes: `LoadedCompilation`, `MsBuildCompilationLoader` (Task 10); `MethodFacts` (Task 5).
- Produces:
  - `class TurtleSyntaxWalker : CSharpSyntaxWalker` with `List<MethodDeclarationSyntax> Methods { get; }`
  - `class SemanticAnalyzer(Compilation compilation)` with `IReadOnlyList<MethodFacts> AnalyzeTree(SyntaxTree tree)`

> Keep `SemanticAnalyzer` ≤300 lines; if it grows, extract `AllocationDetector`/`AsyncHealthDetector` into sibling files.

- [ ] **Step 1: Add the shared compilation fixture**

`tests/CodeTurtleEngine.Gatekeeper.Tests/SampleRepoCompilationFixture.cs`:
```csharp
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public sealed class SampleRepoCompilationFixture : IAsyncLifetime
{
    public LoadedCompilation Loaded { get; private set; } = default!;

    public async Task InitializeAsync()
        => Loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.SampleRepoCsproj);

    public Task DisposeAsync()
    {
        Loaded.Workspace?.Dispose();
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing test**

`tests/CodeTurtleEngine.Gatekeeper.Tests/SemanticAnalyzerTests.cs`:
```csharp
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class SemanticAnalyzerTests(SampleRepoCompilationFixture fx) : IClassFixture<SampleRepoCompilationFixture>
{
    private SyntaxTree PaymentTree() =>
        fx.Loaded.Compilation.SyntaxTrees.First(t => t.FilePath.EndsWith("PaymentService.cs"));

    [Fact]
    public void Detects_Dependencies_And_ResolvedSymbols()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.Contains("IPaymentGateway", m.Dependencies);
        Assert.Contains("ILogger", m.Dependencies);
        Assert.Contains("SampleRepo.IPaymentGateway", m.ResolvedSymbols);
    }

    [Fact]
    public void Detects_Missing_ConfigureAwait()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.NotNull(m.AsyncHealth);
        Assert.Contains("ConfigureAwait", m.AsyncHealth);
    }

    [Fact]
    public void Detects_Boxing_And_Linq_Closure_Allocations()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.Contains(m.Allocations, a => a.Contains("Boxing"));
        Assert.Contains(m.Allocations, a => a.Contains("LINQ Closure"));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~SemanticAnalyzerTests"`
Expected: FAIL — `SemanticAnalyzer` does not exist.

- [ ] **Step 4: Implement the walker**

`src/CodeTurtleEngine.Gatekeeper/TurtleSyntaxWalker.cs`:
```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class TurtleSyntaxWalker : CSharpSyntaxWalker
{
    public List<MethodDeclarationSyntax> Methods { get; } = new();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Methods.Add(node);
        base.VisitMethodDeclaration(node);
    }
}
```

- [ ] **Step 5: Implement the semantic analyzer**

`src/CodeTurtleEngine.Gatekeeper/SemanticAnalyzer.cs`:
```csharp
using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class SemanticAnalyzer
{
    private readonly Compilation _compilation;

    public SemanticAnalyzer(Compilation compilation) => _compilation = compilation;

    public IReadOnlyList<MethodFacts> AnalyzeTree(SyntaxTree tree)
    {
        var model = _compilation.GetSemanticModel(tree);
        var walker = new TurtleSyntaxWalker();
        walker.Visit(tree.GetRoot());

        var facts = new List<MethodFacts>();
        foreach (var method in walker.Methods)
        {
            var dependencies = ConstructorDependencies(method, model);
            var allocations = DetectBoxing(method, model).Concat(DetectLinqClosures(method, model)).ToList();
            var asyncHealth = DetectAsyncHealth(method, model);
            var resolved = ResolvedSymbols(method, model);
            facts.Add(new MethodFacts(method.Identifier.Text, allocations, asyncHealth, dependencies, resolved));
        }
        return facts;
    }

    private static IReadOnlyList<string> ConstructorDependencies(MethodDeclarationSyntax method, SemanticModel model)
    {
        var type = model.GetDeclaredSymbol(method)?.ContainingType;
        return type is null
            ? Array.Empty<string>()
            : type.Constructors.SelectMany(c => c.Parameters).Select(p => p.Type.Name).Distinct().ToList();
    }

    private static string? DetectAsyncHealth(MethodDeclarationSyntax method, SemanticModel model)
    {
        if (!method.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword)) return null;
        foreach (var awaitExpr in method.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            if (awaitExpr.Expression is InvocationExpressionSyntax inv
                && inv.Expression is MemberAccessExpressionSyntax ma
                && ma.Name.Identifier.Text == "ConfigureAwait")
                continue;

            var owner = model.GetSymbolInfo(awaitExpr.Expression).Symbol?.ContainingType?.Name ?? "task";
            return $"Missing ConfigureAwait(false) on {owner} invocation";
        }
        return null;
    }

    private IEnumerable<string> DetectBoxing(MethodDeclarationSyntax method, SemanticModel model)
    {
        var objectType = _compilation.GetSpecialType(SpecialType.System_Object);
        var seen = new HashSet<int>();
        foreach (var expr in method.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var info = model.GetTypeInfo(expr);
            if (info.Type is { IsValueType: true }
                && SymbolEqualityComparer.Default.Equals(info.ConvertedType, objectType))
            {
                var line = expr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                if (seen.Add(line)) yield return $"Implicit Boxing (Line {line})";
            }
        }
    }

    private static IEnumerable<string> DetectLinqClosures(MethodDeclarationSyntax method, SemanticModel model)
    {
        foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol sym) continue;
            if (sym.ContainingNamespace?.ToDisplayString() != "System.Linq") continue;

            var lambda = inv.ArgumentList.Arguments.Select(a => a.Expression)
                .OfType<LambdaExpressionSyntax>().FirstOrDefault();
            if (lambda is null || !CapturesLocal(lambda, model)) continue;

            var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"LINQ Closure (Line {line})";
        }
    }

    private static bool CapturesLocal(LambdaExpressionSyntax lambda, SemanticModel model) =>
        lambda.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => model.GetSymbolInfo(id).Symbol is ILocalSymbol);

    private static IReadOnlyList<string> ResolvedSymbols(MethodDeclarationSyntax method, SemanticModel model)
    {
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var type = model.GetDeclaredSymbol(method)?.ContainingType;
        if (type is not null)
            foreach (var ctor in type.Constructors)
                foreach (var p in ctor.Parameters)
                    if (p.Type is not null) symbols.Add(p.Type.ToDisplayString());

        foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            if (model.GetSymbolInfo(inv).Symbol is IMethodSymbol m && m.ContainingType is not null)
                symbols.Add(m.ContainingType.ToDisplayString());

        return symbols.OrderBy(s => s, StringComparer.Ordinal).ToList();
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~SemanticAnalyzerTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/CodeTurtleEngine.Gatekeeper/TurtleSyntaxWalker.cs src/CodeTurtleEngine.Gatekeeper/SemanticAnalyzer.cs tests/CodeTurtleEngine.Gatekeeper.Tests/SampleRepoCompilationFixture.cs tests/CodeTurtleEngine.Gatekeeper.Tests/SemanticAnalyzerTests.cs
git commit -m "feat: add Roslyn syntax walker and semantic analyzer"
```

---

### Task 12: Gatekeeper `PayloadGenerator`

**Files:**
- Create: `src/CodeTurtleEngine.Gatekeeper/PayloadGenerator.cs`
- Test: `tests/CodeTurtleEngine.Gatekeeper.Tests/PayloadGeneratorTests.cs`

**Interfaces:**
- Consumes: `Compilation` (Task 10), `SemanticAnalyzer` (Task 11), `RoslynPayload`/`FileFacts` (Task 5).
- Produces:
  - `class PayloadGenerator(Compilation compilation)` with `RoslynPayload Build(string repoSlug, string diffBaseline, IReadOnlyList<string> changedRelativePaths)`
  - Changed paths are repo-relative (from `DiffProvider`); matched against `SyntaxTree.FilePath` by suffix. Files with no methods or not in the compilation are skipped.

- [ ] **Step 1: Write the failing test**

`tests/CodeTurtleEngine.Gatekeeper.Tests/PayloadGeneratorTests.cs`:
```csharp
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class PayloadGeneratorTests(SampleRepoCompilationFixture fx) : IClassFixture<SampleRepoCompilationFixture>
{
    [Fact]
    public void Builds_Payload_For_Changed_File()
    {
        var payload = new PayloadGenerator(fx.Loaded.Compilation)
            .Build("SampleRepo", "HEAD", new[] { "PaymentService.cs" });

        Assert.Equal("SampleRepo", payload.RepoSlug);
        Assert.Single(payload.Files);
        Assert.Equal("PaymentService.cs", payload.Files[0].FilePath);
        Assert.Contains(payload.Files[0].Methods, m => m.Method == "ProcessPaymentAsync");
    }

    [Fact]
    public void Skips_Files_Not_In_Compilation()
    {
        var payload = new PayloadGenerator(fx.Loaded.Compilation)
            .Build("SampleRepo", "HEAD", new[] { "DoesNotExist.cs" });

        Assert.Empty(payload.Files);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests --filter "FullyQualifiedName~PayloadGeneratorTests"`
Expected: FAIL — `PayloadGenerator` does not exist.

- [ ] **Step 3: Implement**

`src/CodeTurtleEngine.Gatekeeper/PayloadGenerator.cs`:
```csharp
using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class PayloadGenerator
{
    private readonly Compilation _compilation;
    private readonly SemanticAnalyzer _analyzer;

    public PayloadGenerator(Compilation compilation)
    {
        _compilation = compilation;
        _analyzer = new SemanticAnalyzer(compilation);
    }

    public RoslynPayload Build(string repoSlug, string diffBaseline, IReadOnlyList<string> changedRelativePaths)
    {
        var files = new List<FileFacts>();
        foreach (var rel in changedRelativePaths)
        {
            var tree = _compilation.SyntaxTrees.FirstOrDefault(t => PathsMatch(t.FilePath, rel));
            if (tree is null) continue;

            var methods = _analyzer.AnalyzeTree(tree);
            if (methods.Count == 0) continue;

            files.Add(new FileFacts(rel, methods));
        }
        return new RoslynPayload(repoSlug, diffBaseline, files);
    }

    private static bool PathsMatch(string treePath, string rel)
    {
        var a = treePath.Replace('\\', '/');
        var b = rel.Replace('\\', '/');
        return a.EndsWith(b, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Gatekeeper.Tests`
Expected: PASS (Diff + CompilationLoader + SemanticAnalyzer + PayloadGenerator).

- [ ] **Step 5: Commit**

```bash
git add src/CodeTurtleEngine.Gatekeeper/PayloadGenerator.cs tests/CodeTurtleEngine.Gatekeeper.Tests/PayloadGeneratorTests.cs
git commit -m "feat: add Roslyn payload generator"
```

---

### Task 13: Council `RubricLoader` + `PromptBuilder`

**Files:**
- Create: `src/CodeTurtleEngine.Council/RubricLoader.cs`, `src/CodeTurtleEngine.Council/PromptBuilder.cs`, `src/CodeTurtleEngine.Council/CouncilOptions.cs`, `src/CodeTurtleEngine.Council/VerdictDto.cs`
- Test: `tests/CodeTurtleEngine.Council.Tests/PromptBuilderTests.cs`

**Interfaces:**
- Consumes: `RoslynPayload`, `PersonaRole` (Task 5); `ChatMessage`, `ChatRole` (`Microsoft.Extensions.AI`).
- Produces:
  - `class CouncilOptions { int Quorum = 2; GuardMode Guard = GuardMode.Strip; }`
  - `class RubricLoader { string Load(string path) }` (throws `FileNotFoundException` with a clear message if missing)
  - `static class PromptBuilder`:
    - `IReadOnlyList<ChatMessage> Build(PersonaRole role, RoslynPayload payload, string rubric)` — a `System` message (persona instructions + rubric) and a `User` message (minified payload JSON).
    - `string VerdictSchema { get; }` — JSON schema for `VerdictDto`.
  - `record FindingDto(Severity Severity, string Title, string Detail, string Location, IReadOnlyList<string> CitedSymbolFqns)`
  - `record VerdictDto(IReadOnlyList<FindingDto> Findings)`

- [ ] **Step 1: Write the failing test**

`tests/CodeTurtleEngine.Council.Tests/PromptBuilderTests.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Council.Tests;

public class PromptBuilderTests
{
    private static RoslynPayload SamplePayload() => new("R", "HEAD", new[]
    {
        new FileFacts("DataAccess.cs", new[]
        {
            new MethodFacts("BuildQuery", Array.Empty<string>(), null,
                Array.Empty<string>(), new[] { "SampleRepo.DataAccess" })
        })
    });

    [Fact]
    public void RubricLoader_Reads_File()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "# Review Rubric v1\nBe specific.");
        var text = new RubricLoader().Load(tmp);
        Assert.Contains("Review Rubric", text);
    }

    [Fact]
    public void Security_Prompt_Has_System_Rubric_And_Payload()
    {
        var msgs = PromptBuilder.Build(PersonaRole.SecurityAuditor, SamplePayload(), "RUBRIC-TEXT");

        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("security", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("RUBRIC-TEXT"));
        Assert.Contains(msgs, m => m.Role == ChatRole.User && m.Text.Contains("DataAccess.cs"));
    }

    [Fact]
    public void VerdictSchema_Requires_Findings()
        => Assert.Contains("\"Findings\"", PromptBuilder.VerdictSchema);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests --filter "FullyQualifiedName~PromptBuilderTests"`
Expected: FAIL — `PromptBuilder` does not exist.

- [ ] **Step 3: Implement DTOs and options**

`src/CodeTurtleEngine.Council/VerdictDto.cs`:
```csharp
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record FindingDto(
    Severity Severity,
    string Title,
    string Detail,
    string Location,
    IReadOnlyList<string> CitedSymbolFqns);

public sealed record VerdictDto(IReadOnlyList<FindingDto> Findings);
```
`src/CodeTurtleEngine.Council/CouncilOptions.cs`:
```csharp
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed class CouncilOptions
{
    public int Quorum { get; set; } = 2;
    public GuardMode Guard { get; set; } = GuardMode.Strip;
}
```

- [ ] **Step 4: Implement RubricLoader and PromptBuilder**

`src/CodeTurtleEngine.Council/RubricLoader.cs`:
```csharp
namespace CodeTurtleEngine.Council;

public sealed class RubricLoader
{
    public string Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Rubric not found at '{path}'.", path);
        return File.ReadAllText(path);
    }
}
```
`src/CodeTurtleEngine.Council/PromptBuilder.cs`:
```csharp
using CodeTurtleEngine.Core;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeTurtleEngine.Council;

public static class PromptBuilder
{
    public static string VerdictSchema { get; } = """
    {"type":"object","additionalProperties":false,"properties":{
      "Findings":{"type":"array","items":{
        "type":"object","additionalProperties":false,"properties":{
          "Severity":{"type":"string","enum":["Info","Nit","Warning","Error","Critical"]},
          "Title":{"type":"string"},
          "Detail":{"type":"string"},
          "Location":{"type":"string"},
          "CitedSymbolFqns":{"type":"array","items":{"type":"string"}}
        },"required":["Severity","Title","Detail","Location","CitedSymbolFqns"]}}},
      "required":["Findings"]}
    """;

    public static IReadOnlyList<ChatMessage> Build(PersonaRole role, RoslynPayload payload, string rubric)
    {
        var system = $"{PersonaInstruction(role)}\n\n# Review Rubric\n{rubric}\n\n{GroundingRules}";
        var user = JsonSerializer.Serialize(payload, TurtleJson.Options);
        return new[]
        {
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user)
        };
    }

    private const string GroundingRules =
        "You are given a minified JSON semantic payload extracted by Roslyn. " +
        "Do NOT invent symbols. Every finding MUST cite symbol FQNs that appear in the payload's ResolvedSymbols. " +
        "Return ONLY JSON matching the schema.";

    private static string PersonaInstruction(PersonaRole role) => role switch
    {
        PersonaRole.AllocationsPerformance =>
            "You are a C# allocations and performance reviewer. Scrutinize memory leaks, large-object-heap (LOH) risk, unawaited tasks, and thread-safety issues in concurrent collections.",
        PersonaRole.SecurityAuditor =>
            "You are a C# security auditor. Evaluate input sanitization, SQL-injection vectors (raw string concatenation bypassing EF Core parameters), and authentication-bypass risks on REST/GraphQL endpoints.",
        PersonaRole.IdiomaticArchitect =>
            "You are an idiomatic C# architect. Enforce modern C# language features, clean-architecture boundaries, and correct Dependency Injection lifecycles (e.g., capturing Transient services inside Singleton classes).",
        _ => "You are a C# code reviewer."
    };
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests --filter "FullyQualifiedName~PromptBuilderTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/CodeTurtleEngine.Council/VerdictDto.cs src/CodeTurtleEngine.Council/CouncilOptions.cs src/CodeTurtleEngine.Council/RubricLoader.cs src/CodeTurtleEngine.Council/PromptBuilder.cs tests/CodeTurtleEngine.Council.Tests/PromptBuilderTests.cs
git commit -m "feat: add council rubric loader and persona prompt builder"
```

---

### Task 14: Council `PersonaRunner` (parallel) + `Arbiter`

The Arbiter is **deterministic** in the MVP (dedupe by title+location, keep highest severity, sort, render markdown). LLM-based conflict resolution (performance vs readability) is deferred to Phase 2.

**Files:**
- Create: `src/CodeTurtleEngine.Council/PersonaRunner.cs`, `src/CodeTurtleEngine.Council/Arbiter.cs`
- Test: `tests/CodeTurtleEngine.Council.Tests/TestDoubles.cs`, `PersonaRunnerTests.cs`, `ArbiterTests.cs`

**Interfaces:**
- Consumes: `IStructuredChatClient` (Task 8); `PromptBuilder`, `VerdictDto`, `CouncilOptions` (Task 13); `ProviderException` (Task 6); Core records (Task 5).
- Produces:
  - `interface IPersonaRunner { Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default) }`
  - `class PersonaRunner(IStructuredChatClient chat, CouncilOptions options) : IPersonaRunner`; `static string PersonaRunner.ModelRoleFor(PersonaRole)` → `"fast"` for AllocationsPerformance, `"deep"` otherwise.
  - `interface IArbiter { IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts); string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload) }`
  - `class Arbiter : IArbiter`

- [ ] **Step 1: Write the test double**

`tests/CodeTurtleEngine.Council.Tests/TestDoubles.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Council.Tests;

internal sealed class FakeStructuredChat : IStructuredChatClient
{
    private readonly Func<string, IReadOnlyList<ChatMessage>, object> _responder;

    public List<string> RolesCalled { get; } = new();

    public FakeStructuredChat(Func<string, IReadOnlyList<ChatMessage>, object> responder)
        => _responder = responder;

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        lock (RolesCalled) RolesCalled.Add(role);
        return Task.FromResult((T)_responder(role, messages));
    }
}
```

- [ ] **Step 2: Write the failing PersonaRunner tests**

`tests/CodeTurtleEngine.Council.Tests/PersonaRunnerTests.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Council.Tests;

public class PersonaRunnerTests
{
    private static RoslynPayload Payload() => new("R", "HEAD", new[]
    {
        new FileFacts("DataAccess.cs", new[]
        {
            new MethodFacts("BuildQuery", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "SampleRepo.DataAccess" })
        })
    });

    [Fact]
    public async Task Runs_Three_Personas_And_Maps_Roles()
    {
        var chat = new FakeStructuredChat((role, _) => new VerdictDto(new[]
        {
            new FindingDto(Severity.Warning, "Issue-" + role, "detail", "File.cs:1", new[] { "SampleRepo.DataAccess" })
        }));
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunAsync(Payload(), "rubric");

        Assert.Equal(3, verdicts.Count);
        Assert.Contains(verdicts, v => v.Persona == PersonaRole.SecurityAuditor);
        Assert.Contains("fast", chat.RolesCalled);
        Assert.Equal(2, chat.RolesCalled.Count(r => r == "deep"));
        Assert.All(verdicts, v => Assert.Single(v.Findings));
    }

    [Fact]
    public async Task Proceeds_At_Quorum_When_One_Persona_Fails()
    {
        var call = 0;
        var chat = new FakeStructuredChat((_, _) =>
        {
            if (Interlocked.Increment(ref call) == 1) throw new InvalidOperationException("boom");
            return new VerdictDto(Array.Empty<FindingDto>());
        });
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunAsync(Payload(), "rubric");

        Assert.Equal(2, verdicts.Count);
    }

    [Fact]
    public async Task Throws_ProviderException_Below_Quorum()
    {
        var chat = new FakeStructuredChat((_, _) => throw new InvalidOperationException("down"));
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        await Assert.ThrowsAsync<ProviderException>(() => runner.RunAsync(Payload(), "rubric"));
    }
}
```

- [ ] **Step 3: Run PersonaRunner tests to verify they fail**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests --filter "FullyQualifiedName~PersonaRunnerTests"`
Expected: FAIL — `PersonaRunner` does not exist.

- [ ] **Step 4: Implement PersonaRunner**

`src/CodeTurtleEngine.Council/PersonaRunner.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Council;

public interface IPersonaRunner
{
    Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default);
}

public sealed class PersonaRunner : IPersonaRunner
{
    private static readonly PersonaRole[] ReviewPersonas =
    {
        PersonaRole.AllocationsPerformance,
        PersonaRole.SecurityAuditor,
        PersonaRole.IdiomaticArchitect
    };

    private readonly IStructuredChatClient _chat;
    private readonly CouncilOptions _options;

    public PersonaRunner(IStructuredChatClient chat, CouncilOptions options)
    {
        _chat = chat;
        _options = options;
    }

    public async Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default)
    {
        var results = await Task.WhenAll(ReviewPersonas.Select(role => RunOneAsync(role, payload, rubric, ct)));
        var succeeded = results.Where(r => r is not null).Select(r => r!).ToList();

        if (succeeded.Count < _options.Quorum)
            throw new ProviderException(
                $"Council quorum not met: {succeeded.Count}/{ReviewPersonas.Length} personas succeeded (quorum {_options.Quorum}).",
                new[] { $"{ReviewPersonas.Length - succeeded.Count} persona call(s) failed" });

        return succeeded;
    }

    private async Task<PersonaVerdict?> RunOneAsync(PersonaRole role, RoslynPayload payload, string rubric, CancellationToken ct)
    {
        try
        {
            var msgs = PromptBuilder.Build(role, payload, rubric);
            var dto = await _chat.CompleteStructuredAsync<VerdictDto>(ModelRoleFor(role), msgs, PromptBuilder.VerdictSchema, ct);
            var findings = dto.Findings
                .Select(f => new ReviewFinding(role, f.Severity, f.Title, f.Detail, f.Location, f.CitedSymbolFqns))
                .ToList();
            return new PersonaVerdict(role, findings);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string ModelRoleFor(PersonaRole role) =>
        role == PersonaRole.AllocationsPerformance ? "fast" : "deep";
}
```

- [ ] **Step 5: Run PersonaRunner tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests --filter "FullyQualifiedName~PersonaRunnerTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Write the failing Arbiter tests**

`tests/CodeTurtleEngine.Council.Tests/ArbiterTests.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class ArbiterTests
{
    [Fact]
    public void Merge_Dedupes_By_Title_Location_Keeping_Highest_Severity()
    {
        var verdicts = new[]
        {
            new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
            {
                new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "SQL injection", "d", "DataAccess.cs:11", new[] { "A" })
            }),
            new PersonaVerdict(PersonaRole.IdiomaticArchitect, new[]
            {
                new ReviewFinding(PersonaRole.IdiomaticArchitect, Severity.Error, "sql injection", "d2", "DataAccess.cs:11", new[] { "A" })
            })
        };

        var merged = new Arbiter().Merge(verdicts);

        Assert.Single(merged);
        Assert.Equal(Severity.Error, merged[0].Severity);
    }

    [Fact]
    public void RenderMarkdown_Includes_Title_Severity_Location()
    {
        var findings = new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection", "Raw concat.", "DataAccess.cs:11", new[] { "SampleRepo.DataAccess" })
        };

        var md = new Arbiter().RenderMarkdown(findings, new RoslynPayload("SampleRepo", "HEAD", Array.Empty<FileFacts>()));

        Assert.Contains("## [Error] SQL injection", md);
        Assert.Contains("DataAccess.cs:11", md);
    }
}
```

- [ ] **Step 7: Implement Arbiter**

`src/CodeTurtleEngine.Council/Arbiter.cs`:
```csharp
using CodeTurtleEngine.Core;
using System.Text;

namespace CodeTurtleEngine.Council;

public interface IArbiter
{
    IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts);
    string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload);
}

public sealed class Arbiter : IArbiter
{
    public IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts) =>
        verdicts.SelectMany(v => v.Findings)
            .GroupBy(f => (Normalize(f.Title), f.Location))
            .Select(g => g.OrderByDescending(f => (int)f.Severity).First())
            .OrderByDescending(f => (int)f.Severity)
            .ThenBy(f => f.Location, StringComparer.Ordinal)
            .ToList();

    public string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Code Turtle Review - {payload.RepoSlug}");
        sb.AppendLine();
        sb.AppendLine($"Diff baseline: `{payload.DiffBaseline}` - Files reviewed: {payload.Files.Count}");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("_No issues found._");
            return sb.ToString();
        }

        foreach (var f in findings)
        {
            sb.AppendLine($"## [{f.Severity}] {f.Title}");
            sb.AppendLine($"- **Persona:** {f.Persona}");
            sb.AppendLine($"- **Location:** `{f.Location}`");
            sb.AppendLine($"- {f.Detail}");
            if (f.CitedSymbolFqns.Count > 0)
                sb.AppendLine($"- **Symbols:** {string.Join(", ", f.CitedSymbolFqns.Select(s => $"`{s}`"))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Normalize(string title) => title.Trim().ToLowerInvariant();
}
```

- [ ] **Step 8: Run council tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests`
Expected: PASS (PromptBuilder + PersonaRunner + Arbiter).

- [ ] **Step 9: Commit**

```bash
git add src/CodeTurtleEngine.Council/PersonaRunner.cs src/CodeTurtleEngine.Council/Arbiter.cs tests/CodeTurtleEngine.Council.Tests/TestDoubles.cs tests/CodeTurtleEngine.Council.Tests/PersonaRunnerTests.cs tests/CodeTurtleEngine.Council.Tests/ArbiterTests.cs
git commit -m "feat: add parallel persona runner and deterministic arbiter"
```

---

### Task 15: Council `TurtleShellGuard` (zero-hallucination)

Verifies every finding's cited symbol FQN against the payload's `ResolvedSymbols` allow-list (the symbols Roslyn actually resolved). In `Strip` mode, unverified citations are removed and a finding left with no verified citation (when it cited symbols) is dropped. In `Flag` mode, findings are kept and the audit records the unverified citations.

**Files:**
- Create: `src/CodeTurtleEngine.Council/TurtleShellGuard.cs`
- Test: `tests/CodeTurtleEngine.Council.Tests/TurtleShellGuardTests.cs`

**Interfaces:**
- Consumes: `ReviewFinding`, `RoslynPayload`, `GuardResult`, `GuardMode` (Task 5); `CouncilOptions` (Task 13).
- Produces:
  - `record GuardOutcome(IReadOnlyList<ReviewFinding> KeptFindings, IReadOnlyList<GuardResult> Audit)`
  - `interface ITurtleShellGuard { GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload) }`
  - `class TurtleShellGuard(CouncilOptions options) : ITurtleShellGuard`

- [ ] **Step 1: Write the failing test**

`tests/CodeTurtleEngine.Council.Tests/TurtleShellGuardTests.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class TurtleShellGuardTests
{
    private static RoslynPayload PayloadWith(string resolvedSymbol) => new("R", "HEAD", new[]
    {
        new FileFacts("F.cs", new[]
        {
            new MethodFacts("M", Array.Empty<string>(), null, Array.Empty<string>(), new[] { resolvedSymbol })
        })
    });

    private static ReviewFinding Finding(string title, params string[] cites) =>
        new(PersonaRole.SecurityAuditor, Severity.Error, title, "d", "F.cs:1", cites);

    [Fact]
    public void Strip_Removes_Hallucinated_Citations_And_Ungrounded_Findings()
    {
        var findings = new[]
        {
            Finding("Real issue", "SampleRepo.Real"),
            Finding("Hallucinated", "SampleRepo.Fake"),
            Finding("Mixed", "SampleRepo.Real", "SampleRepo.Fake")
        };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Equal(2, outcome.KeptFindings.Count);
        Assert.Contains(outcome.KeptFindings, f => f.Title == "Real issue");
        Assert.DoesNotContain(outcome.KeptFindings, f => f.Title == "Hallucinated");
        Assert.Equal(new[] { "SampleRepo.Real" }, outcome.KeptFindings.First(f => f.Title == "Mixed").CitedSymbolFqns);
        Assert.Contains(outcome.Audit, a => a.CitedFqn == "SampleRepo.Fake" && !a.Verified);
    }

    [Fact]
    public void Flag_Keeps_Findings_And_Records_Audit()
    {
        var findings = new[] { Finding("Hallucinated", "SampleRepo.Fake") };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Flag });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Single(outcome.KeptFindings);
        Assert.Contains(outcome.Audit, a => a.CitedFqn == "SampleRepo.Fake" && !a.Verified);
    }

    [Fact]
    public void Finding_With_No_Citations_Is_Kept_In_Strip_Mode()
    {
        var findings = new[] { Finding("General note") };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Single(outcome.KeptFindings);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests --filter "FullyQualifiedName~TurtleShellGuardTests"`
Expected: FAIL — `TurtleShellGuard` does not exist.

- [ ] **Step 3: Implement**

`src/CodeTurtleEngine.Council/TurtleShellGuard.cs`:
```csharp
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record GuardOutcome(
    IReadOnlyList<ReviewFinding> KeptFindings,
    IReadOnlyList<GuardResult> Audit);

public interface ITurtleShellGuard
{
    GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload);
}

public sealed class TurtleShellGuard : ITurtleShellGuard
{
    private readonly CouncilOptions _options;

    public TurtleShellGuard(CouncilOptions options) => _options = options;

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload)
    {
        var allow = payload.Files
            .SelectMany(f => f.Methods)
            .SelectMany(m => m.ResolvedSymbols)
            .ToHashSet(StringComparer.Ordinal);

        var audit = new List<GuardResult>();
        var kept = new List<ReviewFinding>();

        foreach (var finding in findings)
        {
            var verified = new List<string>();
            foreach (var cite in finding.CitedSymbolFqns)
            {
                var ok = allow.Contains(cite);
                audit.Add(new GuardResult(cite, ok));
                if (ok) verified.Add(cite);
            }

            if (_options.Guard == GuardMode.Flag)
            {
                kept.Add(finding);
                continue;
            }

            // Strip mode: drop a finding that cited symbols but none verified.
            if (finding.CitedSymbolFqns.Count > 0 && verified.Count == 0) continue;
            kept.Add(finding with { CitedSymbolFqns = verified });
        }

        return new GuardOutcome(kept, audit);
    }
}
```

- [ ] **Step 4: Run council tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Council.Tests`
Expected: PASS (PromptBuilder + PersonaRunner + Arbiter + TurtleShellGuard).

- [ ] **Step 5: Commit**

```bash
git add src/CodeTurtleEngine.Council/TurtleShellGuard.cs tests/CodeTurtleEngine.Council.Tests/TurtleShellGuardTests.cs
git commit -m "feat: add Turtle Shell guard enforcing zero-hallucination citations"
```

---

### Task 16: Cli — options, `ArtifactWriter`, `ReviewPipeline`, `Program`/DI

Wires the whole pipeline behind `turtle review <repo> [--project <path>] [--diff <ref>] [--out <file>]`. Fails closed (compilation errors abort before the council). Exit code 0 on success, 1 on error.

**Files:**
- Create: `src/CodeTurtleEngine.Cli/TurtleOptions.cs`, `ArtifactWriter.cs`, `ReviewPipeline.cs`, `Composition.cs`, `appsettings.json`; replace `Program.cs`
- Modify: `src/CodeTurtleEngine.Cli/CodeTurtleEngine.Cli.csproj` (copy `appsettings.json` to output)
- Test: `tests/CodeTurtleEngine.Cli.Tests/TestDoubles.cs`, `ArtifactWriterTests.cs`, `ReviewPipelineTests.cs`, `CompositionTests.cs`

**Interfaces:**
- Consumes: `IDiffProvider`/`ICompilationLoader`/`LoadedCompilation` (Tasks 9–10), `PayloadGenerator` (Task 12), `RubricLoader`/`IPersonaRunner`/`IArbiter`/`ITurtleShellGuard`/`CouncilOptions` (Tasks 13–15), `ILlmGateway`/`IStructuredChatClient`/`IChatClientFactory`/`LlmOptions` (Tasks 7–8), Core records (Task 5).
- Produces:
  - `class TurtleOptions { const string SectionName = "Turtle"; string RubricPath; CouncilOptions Council }`
  - `class ArtifactWriter(string root)` with `string Write(string repoSlug, RoslynPayload payload, CouncilVerdict verdict, string markdown)` → returns the artifact directory.
  - `record ReviewResult(string Markdown, CouncilVerdict Verdict, string ArtifactDir)`
  - `class ReviewPipeline(...)` with `Task<ReviewResult> RunAsync(string repoPath, string projectPath, string? baselineRef, CancellationToken ct = default)`
  - `static class Composition { IServiceProvider BuildServices(string repoPath); string DiscoverProject(string repoPath) }`

> **API note:** `System.CommandLine` is prerelease; its `SetHandler` overload set changes between betas. If the 4-binding overload is unavailable, nest `SetHandler` or read `InvocationContext.ParseResult`. Keep the command surface identical.

- [ ] **Step 1: Add packages and output-copy**

Run:
```bash
dotnet add src/CodeTurtleEngine.Cli package System.CommandLine --prerelease
dotnet add src/CodeTurtleEngine.Cli package Microsoft.Extensions.Configuration.Json
dotnet add src/CodeTurtleEngine.Cli package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add src/CodeTurtleEngine.Cli package Microsoft.Extensions.Configuration.Binder
dotnet add src/CodeTurtleEngine.Cli package Microsoft.Extensions.DependencyInjection
```
In `src/CodeTurtleEngine.Cli/CodeTurtleEngine.Cli.csproj` add:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: Add options and config**

`src/CodeTurtleEngine.Cli/TurtleOptions.cs`:
```csharp
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Cli;

public sealed class TurtleOptions
{
    public const string SectionName = "Turtle";
    public string RubricPath { get; set; } = "turtle/rubric_v1.md";
    public CouncilOptions Council { get; set; } = new();
}
```
`src/CodeTurtleEngine.Cli/appsettings.json`:
```json
{
  "Llm": {
    "Routes": [
      { "Name": "bailian-deep", "BaseUrlEnv": "TURTLE_LLM_BASE_URL", "ApiKeyEnv": "TURTLE_LLM_API_KEY", "Model": "qwen3.8-max" },
      { "Name": "bailian-fast", "BaseUrlEnv": "TURTLE_LLM_BASE_URL", "ApiKeyEnv": "TURTLE_LLM_API_KEY", "Model": "qwen3.8-flash" }
    ],
    "ModelRoles": { "deep": "qwen3.8-max", "fast": "qwen3.8-flash" }
  },
  "Turtle": {
    "RubricPath": "turtle/rubric_v1.md",
    "Council": { "Quorum": 2, "Guard": "Strip" }
  }
}
```

- [ ] **Step 3: Write the failing ArtifactWriter test**

`tests/CodeTurtleEngine.Cli.Tests/ArtifactWriterTests.cs`:
```csharp
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Cli.Tests;

public class ArtifactWriterTests
{
    [Fact]
    public void Writes_All_Artifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-art-" + Guid.NewGuid().ToString("N"));
        var writer = new ArtifactWriter(root);
        var payload = new RoslynPayload("Sample.Repo", "HEAD", Array.Empty<FileFacts>());
        var verdict = new CouncilVerdict(Array.Empty<PersonaVerdict>(), Array.Empty<ReviewFinding>(),
            new[] { new GuardResult("X", true) });

        var dir = writer.Write("Sample.Repo", payload, verdict, "# review");

        Assert.True(File.Exists(Path.Combine(dir, "review.md")));
        Assert.True(File.Exists(Path.Combine(dir, "payload.json")));
        Assert.True(File.Exists(Path.Combine(dir, "verdicts.json")));
        Assert.True(File.Exists(Path.Combine(dir, "audit.json")));
        Directory.Delete(root, true);
    }
}
```

- [ ] **Step 4: Implement ArtifactWriter**

`src/CodeTurtleEngine.Cli/ArtifactWriter.cs`:
```csharp
using CodeTurtleEngine.Core;
using System.Text.Json;

namespace CodeTurtleEngine.Cli;

public sealed class ArtifactWriter
{
    private readonly string _root;

    public ArtifactWriter(string root) => _root = root;

    public string Write(string repoSlug, RoslynPayload payload, CouncilVerdict verdict, string markdown)
    {
        var dir = Path.Combine(_root, "reviews", Slugify(repoSlug), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "payload.json"), JsonSerializer.Serialize(payload, TurtleJson.Options));
        File.WriteAllText(Path.Combine(dir, "verdicts.json"), JsonSerializer.Serialize(verdict.Personas, TurtleJson.Options));
        File.WriteAllText(Path.Combine(dir, "review.md"), markdown);
        File.WriteAllText(Path.Combine(dir, "audit.json"), JsonSerializer.Serialize(verdict.GuardAudit, TurtleJson.Options));

        return dir;
    }

    private static string Slugify(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
```

- [ ] **Step 5: Run ArtifactWriter test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Cli.Tests --filter "FullyQualifiedName~ArtifactWriterTests"`
Expected: PASS.

- [ ] **Step 6: Write the pipeline test doubles and failing test**

`tests/CodeTurtleEngine.Cli.Tests/TestDoubles.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests;

internal sealed class FakeDiff : IDiffProvider
{
    private readonly string[] _files;
    public FakeDiff(string[] files) => _files = files;
    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef) => _files;
}

internal sealed class FakeLoader : ICompilationLoader
{
    public Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default)
        => Task.FromResult(new LoadedCompilation(CSharpCompilation.Create("Stub"), new Noop()));

    private sealed class Noop : IDisposable { public void Dispose() { } }
}

internal sealed class FakePersonas : IPersonaRunner
{
    public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PersonaVerdict>>(new[]
        {
            new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
            {
                new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection",
                    "raw concat", "DataAccess.cs:11", new[] { "SampleRepo.DataAccess" })
            })
        });
}
```
`tests/CodeTurtleEngine.Cli.Tests/ReviewPipelineTests.cs`:
```csharp
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli.Tests;

public class ReviewPipelineTests
{
    [Fact]
    public async Task Orchestrates_And_Writes_Artifacts()
    {
        var home = Path.Combine(Path.GetTempPath(), "turtle-pipe-" + Guid.NewGuid().ToString("N"));
        var rubricPath = Path.GetTempFileName();
        File.WriteAllText(rubricPath, "# Review Rubric v1");
        var options = new TurtleOptions { RubricPath = rubricPath, Council = new CouncilOptions { Quorum = 2, Guard = GuardMode.Strip } };

        var pipeline = new ReviewPipeline(
            new FakeDiff(new[] { "PaymentService.cs" }),
            new FakeLoader(),
            new FakePersonas(),
            new Arbiter(),
            new TurtleShellGuard(options.Council),
            new RubricLoader(),
            new ArtifactWriter(home),
            options);

        var result = await pipeline.RunAsync("/repo", "/repo/SampleRepo.csproj", null);

        Assert.Contains("# Code Turtle Review", result.Markdown);
        Assert.True(File.Exists(Path.Combine(result.ArtifactDir, "review.md")));
        Directory.Delete(home, true);
    }
}
```

- [ ] **Step 7: Run pipeline test to verify it fails**

Run: `dotnet test tests/CodeTurtleEngine.Cli.Tests --filter "FullyQualifiedName~ReviewPipelineTests"`
Expected: FAIL — `ReviewPipeline` does not exist.

- [ ] **Step 8: Implement ReviewPipeline**

`src/CodeTurtleEngine.Cli/ReviewPipeline.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli;

public sealed record ReviewResult(string Markdown, CouncilVerdict Verdict, string ArtifactDir);

public sealed class ReviewPipeline
{
    private readonly IDiffProvider _diff;
    private readonly ICompilationLoader _loader;
    private readonly IPersonaRunner _personas;
    private readonly IArbiter _arbiter;
    private readonly ITurtleShellGuard _guard;
    private readonly RubricLoader _rubricLoader;
    private readonly ArtifactWriter _artifacts;
    private readonly TurtleOptions _options;

    public ReviewPipeline(IDiffProvider diff, ICompilationLoader loader, IPersonaRunner personas,
        IArbiter arbiter, ITurtleShellGuard guard, RubricLoader rubricLoader, ArtifactWriter artifacts, TurtleOptions options)
    {
        _diff = diff; _loader = loader; _personas = personas; _arbiter = arbiter;
        _guard = guard; _rubricLoader = rubricLoader; _artifacts = artifacts; _options = options;
    }

    public async Task<ReviewResult> RunAsync(string repoPath, string projectPath, string? baselineRef, CancellationToken ct = default)
    {
        var changed = _diff.GetChangedCSharpFiles(repoPath, baselineRef);
        var loaded = await _loader.LoadAsync(projectPath, ct);
        try
        {
            var repoSlug = Path.GetFileNameWithoutExtension(projectPath);
            var payload = new PayloadGenerator(loaded.Compilation).Build(repoSlug, baselineRef ?? "working-tree", changed);
            var rubric = _rubricLoader.Load(ResolveRubric());
            var verdicts = await _personas.RunAsync(payload, rubric, ct);
            var merged = _arbiter.Merge(verdicts);
            var guarded = _guard.Verify(merged, payload);
            var markdown = _arbiter.RenderMarkdown(guarded.KeptFindings, payload);
            var verdict = new CouncilVerdict(verdicts, guarded.KeptFindings, guarded.Audit);
            var dir = _artifacts.Write(repoSlug, payload, verdict, markdown);
            return new ReviewResult(markdown, verdict, dir);
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }

    private string ResolveRubric() =>
        Path.IsPathRooted(_options.RubricPath)
            ? _options.RubricPath
            : Path.Combine(Directory.GetCurrentDirectory(), _options.RubricPath);
}
```

- [ ] **Step 9: Run pipeline test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Cli.Tests --filter "FullyQualifiedName~ReviewPipelineTests"`
Expected: PASS.

- [ ] **Step 10: Implement Composition + Program**

`src/CodeTurtleEngine.Cli/Composition.cs`:
```csharp
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Cli;

public static class Composition
{
    public static IServiceProvider BuildServices(string repoPath)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var llm = config.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        var turtle = config.GetSection(TurtleOptions.SectionName).Get<TurtleOptions>() ?? new TurtleOptions();
        var home = Environment.GetEnvironmentVariable("TURTLE_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".turtle");

        var sc = new ServiceCollection();
        sc.AddSingleton(llm);
        sc.AddSingleton(turtle);
        sc.AddSingleton<IChatClientFactory, BailianChatClientFactory>();
        sc.AddSingleton<ILlmGateway>(sp => new LlmGateway(sp.GetRequiredService<LlmOptions>(), sp.GetRequiredService<IChatClientFactory>()));
        sc.AddSingleton<IStructuredChatClient>(sp => new StructuredChatClient(sp.GetRequiredService<ILlmGateway>()));
        sc.AddSingleton<IDiffProvider, GitDiffProvider>();
        sc.AddSingleton<ICompilationLoader, MsBuildCompilationLoader>();
        sc.AddSingleton<IPersonaRunner>(sp => new PersonaRunner(sp.GetRequiredService<IStructuredChatClient>(), sp.GetRequiredService<TurtleOptions>().Council));
        sc.AddSingleton<IArbiter, Arbiter>();
        sc.AddSingleton<ITurtleShellGuard>(sp => new TurtleShellGuard(sp.GetRequiredService<TurtleOptions>().Council));
        sc.AddSingleton<RubricLoader>();
        sc.AddSingleton(new ArtifactWriter(home));
        sc.AddSingleton<ReviewPipeline>();
        return sc.BuildServiceProvider();
    }

    public static string DiscoverProject(string repoPath)
    {
        var sln = Directory.EnumerateFiles(repoPath, "*.sln").FirstOrDefault();
        if (sln is not null) return sln;
        var csproj = Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        return csproj ?? throw new DiffException($"No .sln or .csproj found under '{repoPath}'.");
    }
}
```
`src/CodeTurtleEngine.Cli/Program.cs`:
```csharp
using System.CommandLine;
using CodeTurtleEngine.Cli;
using Microsoft.Extensions.DependencyInjection;

var repoArg = new Argument<string>("repo", "Path to the target git repository");
var projectOpt = new Option<string?>("--project", "Path to the .csproj/.sln to compile (default: discovered in repo)");
var diffOpt = new Option<string?>("--diff", "Baseline ref (branch or SHA). Default: working tree vs HEAD");
var outOpt = new Option<string?>("--out", "Also write the markdown review to this path");

var review = new Command("review", "Review a repository's changed C# files") { repoArg, projectOpt, diffOpt, outOpt };
review.SetHandler(async (string repo, string? project, string? diff, string? outPath) =>
{
    try
    {
        var services = Composition.BuildServices(repo);
        var pipeline = services.GetRequiredService<ReviewPipeline>();
        var projectPath = project ?? Composition.DiscoverProject(repo);
        var result = await pipeline.RunAsync(repo, projectPath, diff);

        Console.Out.WriteLine(result.Markdown);
        if (outPath is not null) File.WriteAllText(outPath, result.Markdown);
        Console.Error.WriteLine($"Artifacts: {result.ArtifactDir}");
        Environment.ExitCode = 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        Environment.ExitCode = 1;
    }
}, repoArg, projectOpt, diffOpt, outOpt);

var root = new RootCommand("Code Turtle Engine - AI PR reviewer grounded in Roslyn compiler truths");
root.AddCommand(review);
return await root.InvokeAsync(args);
```

- [ ] **Step 11: Write the failing composition test**

`tests/CodeTurtleEngine.Cli.Tests/CompositionTests.cs`:
```csharp
using CodeTurtleEngine.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Cli.Tests;

public class CompositionTests
{
    [Fact]
    public void DI_Resolves_ReviewPipeline()
    {
        var services = Composition.BuildServices(".");
        Assert.NotNull(services.GetRequiredService<ReviewPipeline>());
    }
}
```

- [ ] **Step 12: Run all Cli tests to verify they pass**

Run: `dotnet test tests/CodeTurtleEngine.Cli.Tests`
Expected: PASS (ArtifactWriter + ReviewPipeline + Composition).

- [ ] **Step 13: Build the whole solution**

Run: `dotnet build`
Expected: succeeds with zero warnings-as-errors.

- [ ] **Step 14: Commit**

```bash
git add src/CodeTurtleEngine.Cli tests/CodeTurtleEngine.Cli.Tests
git commit -m "feat: add CLI review command, pipeline orchestration, and DI composition"
```

---

### Task 17: End-to-end integration test + project docs

Proves the core value path offline — a real Roslyn compilation grounds the council and the guard strips a hallucinated citation — plus a gated live smoke test and the project docs.

**Files:**
- Create: `tests/CodeTurtleEngine.Integration.Tests/TestPaths.cs`, `CitingChat.cs`, `EndToEndTests.cs`, `LiveSmokeTests.cs`
- Create: `turtle/rubric_v1.md`, `README.md`, `AGENTS.md`, `NOTES.md`

**Interfaces:**
- Consumes: the whole stack (Tasks 5–16).
- Produces: a passing offline integration test and committed docs.

- [ ] **Step 1: Add the integration test path helper**

`tests/CodeTurtleEngine.Integration.Tests/TestPaths.cs`:
```csharp
namespace CodeTurtleEngine.Integration.Tests;

public static class TestPaths
{
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodeTurtleEngine.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repo root (sln) not found.");
        }
    }

    public static string SampleRepoCsproj =>
        Path.Combine(RepoRoot, "tests", "fixtures", "SampleRepo", "SampleRepo.csproj");
}
```

- [ ] **Step 2: Add the payload-citing mock chat**

`tests/CodeTurtleEngine.Integration.Tests/CitingChat.cs`:
```csharp
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Integration.Tests;

// Returns one grounded finding (cites a real symbol) and one hallucinated finding.
internal sealed class CitingChat : IStructuredChatClient
{
    private readonly string _realSymbol;

    public CitingChat(string realSymbol) => _realSymbol = realSymbol;

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        var dto = new VerdictDto(new[]
        {
            new FindingDto(Severity.Warning, "Async health", "Missing ConfigureAwait(false).",
                "PaymentService.cs:14", new[] { _realSymbol }),
            new FindingDto(Severity.Error, "Phantom API", "Calls a method that does not exist.",
                "PaymentService.cs:99", new[] { "SampleRepo.DoesNotExist" })
        });
        return Task.FromResult((T)(object)dto);
    }
}
```

- [ ] **Step 3: Write the failing end-to-end test**

`tests/CodeTurtleEngine.Integration.Tests/EndToEndTests.cs`:
```csharp
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Integration.Tests;

public class EndToEndTests
{
    [Fact]
    public async Task Council_Is_Grounded_And_Guard_Strips_Hallucination()
    {
        var loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.SampleRepoCsproj);
        try
        {
            var payload = new PayloadGenerator(loaded.Compilation)
                .Build("SampleRepo", "HEAD", new[] { "PaymentService.cs" });

            var realSymbol = payload.Files
                .SelectMany(f => f.Methods)
                .SelectMany(m => m.ResolvedSymbols)
                .First(s => s.Contains("IPaymentGateway"));

            var options = new CouncilOptions { Quorum = 2, Guard = GuardMode.Strip };
            var verdicts = await new PersonaRunner(new CitingChat(realSymbol), options)
                .RunAsync(payload, "# Review Rubric v1");

            var merged = new Arbiter().Merge(verdicts);
            var guarded = new TurtleShellGuard(options).Verify(merged, payload);

            Assert.Contains(guarded.Audit, a => a.CitedFqn == realSymbol && a.Verified);
            Assert.Contains(guarded.Audit, a => a.CitedFqn == "SampleRepo.DoesNotExist" && !a.Verified);
            Assert.All(guarded.KeptFindings, f => Assert.DoesNotContain("SampleRepo.DoesNotExist", f.CitedSymbolFqns));
            Assert.Contains(guarded.KeptFindings, f => f.Title == "Async health");
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CodeTurtleEngine.Integration.Tests --filter "FullyQualifiedName~EndToEndTests"`
Expected: PASS. (Slow the first time: builds the fixture via MSBuildWorkspace.)

- [ ] **Step 5: Add the gated live smoke test**

`tests/CodeTurtleEngine.Integration.Tests/LiveSmokeTests.cs`:
```csharp
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Integration.Tests;

public class LiveSmokeTests
{
    [Fact]
    public async Task Bailian_Reachable_When_Live_Enabled()
    {
        if (Environment.GetEnvironmentVariable("TURTLE_LIVE") != "1") return; // skipped by default

        var options = new LlmOptions
        {
            Routes = { new RouteOptions { Name = "bailian", BaseUrlEnv = "TURTLE_LLM_BASE_URL", ApiKeyEnv = "TURTLE_LLM_API_KEY", Model = "qwen3.8-flash" } },
            ModelRoles = { ["fast"] = "qwen3.8-flash" }
        };
        var gw = new LlmGateway(options, new BailianChatClientFactory());
        var reply = await gw.CompleteAsync("fast", new[] { new ChatMessage(ChatRole.User, "Reply with the single word: pong") });

        Assert.False(string.IsNullOrWhiteSpace(reply));
    }
}
```

- [ ] **Step 6: Write the frozen rubric**

`turtle/rubric_v1.md`:
```markdown
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
```

- [ ] **Step 7: Write README, AGENTS, NOTES**

`README.md`:
```markdown
# Code-Turtle-Engine

AI pull-request reviewer that grounds LLM reasoning in deterministic Roslyn compiler
truths. A full-repo compilation feeds a minified semantic payload to a council of AI
personas; a Turtle Shell guard verifies every cited symbol so the review cannot
hallucinate APIs.

## MVP scope
Local CLI only: `diff -> compile -> payload -> council -> guard -> markdown`.
Cloud, CI, GitHub, and MCP integrations are on the roadmap (see the design spec).

## Prerequisites
- .NET 10 SDK
- A C# repository to review (must compile)
- Alibaba Bailian (Token Plan) OpenAI-compatible endpoint credentials

## Setup
```bash
export TURTLE_LLM_BASE_URL="https://<bailian-compatible-endpoint>/v1"
export TURTLE_LLM_API_KEY="<key>"
# optional: export TURTLE_HOME="$HOME/.turtle"
```

## Build & test
```bash
dotnet build
dotnet test                       # offline suite
TURTLE_LIVE=1 dotnet test          # includes live Bailian smoke
```

## Run
```bash
dotnet run --project src/CodeTurtleEngine.Cli -- review /path/to/repo \
  --diff HEAD~1 --out review.md
```

## Docs
- Design spec: `docs/superpowers/specs/2026-09-08-code-turtle-engine-design.md`
- Implementation plan: `docs/superpowers/plans/2026-09-08-code-turtle-engine.md`
- Agent-ops contract: `AGENTS.md`
```

`AGENTS.md`:
```markdown
# AGENTS.md — Code-Turtle-Engine

## Architecture map
- `CodeTurtleEngine.Core` — domain records (payload, findings, verdicts). No deps.
- `CodeTurtleEngine.Llm` — gateway: Bailian/Qwen via Microsoft.Extensions.AI, fallback, Polly, structured output, MockChatClient.
- `CodeTurtleEngine.Gatekeeper` — Roslyn: DiffProvider (LibGit2Sharp), MsBuildCompilationLoader, TurtleSyntaxWalker, SemanticAnalyzer, PayloadGenerator.
- `CodeTurtleEngine.Council` — RubricLoader, PromptBuilder, PersonaRunner (parallel), Arbiter (deterministic merge+markdown), TurtleShellGuard (zero-hallucination).
- `CodeTurtleEngine.Cli` — System.CommandLine `review` command, ReviewPipeline orchestration, ArtifactWriter, Composition (DI), TurtleOptions.

Dependency flow: Cli -> {Council, Gatekeeper, Llm} -> Core; Council -> Gatekeeper.

## Commands
- Build: `dotnet build`
- Test (offline): `dotnet test`
- Test (live Bailian): `TURTLE_LIVE=1 dotnet test`
- Run: `dotnet run --project src/CodeTurtleEngine.Cli -- review <repo> [--diff <ref>] [--project <path>] [--out <file>]`

## Invariants
- ≤300 lines per source file.
- Payload-only to the LLM; never send raw source.
- Fail closed: no compilation ⇒ no review.
- Secrets only in env (`TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`); config references env by name.
- TDD: failing test first; commit each task.

## Env
- `TURTLE_LLM_BASE_URL`, `TURTLE_LLM_API_KEY`, `TURTLE_HOME` (default `~/.turtle`), `TURTLE_LIVE=1` (live smoke).

## Agent protocol
1. Query codebase-memory MCP / graph tools before reading files broadly.
2. Check `NOTES.md` for current build state.
3. Tests-first; run the offline suite; keep it green before committing.
```

`NOTES.md`:
```markdown
# NOTES — build log

## 2026-09-08 — plan written
- Design spec + implementation plan committed.
- Stack: .NET 10, Roslyn full-repo compilation, Bailian/Qwen via Microsoft.Extensions.AI + Semantic Kernel, xUnit.
- MVP = local CLI: diff -> compile -> payload -> council -> guard -> markdown.
- Next: execute Task 1 (scaffold), then Spike A (MSBuildWorkspace on net10) and Spike B (Bailian structured output).
```

- [ ] **Step 8: Run the full suite**

Run: `dotnet test`
Expected: all projects green offline (live smoke self-skips without `TURTLE_LIVE=1`).

- [ ] **Step 9: Commit**

```bash
git add tests/CodeTurtleEngine.Integration.Tests turtle/rubric_v1.md README.md AGENTS.md NOTES.md
git commit -m "test: add end-to-end integration + live smoke; docs: README, AGENTS, NOTES, rubric"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §3 layout→Task 1; §4 data flow→Tasks 9–17 composed in `ReviewPipeline`; §5 gatekeeper→Tasks 10–12; §6 council+gateway→Tasks 6–8,13–14; §7 guard→Task 15; §8 config/secrets→Task 16; §9 errors→Tasks 6,8,9,10,16; §10 persistence→Task 16 (ArtifactWriter); §11 testing→per-task + Task 17; §12 roadmap→out of MVP scope (documented in spec). Spikes→Tasks 2–3.
- **Type consistency:** `LoadedCompilation(Compilation, IDisposable Workspace)` used identically in Tasks 10, 16, 17. `CouncilOptions{Quorum,Guard}` in Tasks 13–17. `VerdictDto`/`FindingDto` in Tasks 13,14,17. `GuardOutcome{KeptFindings,Audit}` in Tasks 15–17. `ReviewResult{Markdown,Verdict,ArtifactDir}` in Task 16. Model role strings `"deep"`/`"fast"` consistent (Tasks 8,14,16).
- **Placeholder scan:** none — every code step has full source; every test has assertions; run commands and expected outcomes given.
- **Known risks flagged inline:** MSBuildWorkspace-on-net10 (Spike A decides), Microsoft.Extensions.AI surface drift (noted in Tasks 7–8), System.CommandLine prerelease `SetHandler` (noted Task 16), LibGit2Sharp overload drift (noted Task 9).
