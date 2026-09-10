using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Models;

namespace CodeTurtleEngine.Web.Services;

public interface IWebReviewPipeline
{
    Task ExecuteAsync(Guid jobId, CancellationToken ct = default);
    Task RunAsync(Guid jobId, CancellationToken ct = default) => ExecuteAsync(jobId, ct);
}

public sealed class WebReviewPipeline : IWebReviewPipeline
{
    private readonly IGitHubPrFetcher _prFetcher;
    private readonly IRepoCloner _repoCloner;
    private readonly ICompilationLoader _loader;
    private readonly IStreamingPersonaRunner _personaRunner;
    private readonly IArbiter _arbiter;
    private readonly ITurtleShellGuard _guard;
    private readonly RubricLoader _rubricLoader;
    private readonly IReviewJobStore _jobStore;
    private readonly CouncilOptions _councilOptions;
    private readonly string _rubricPath;

    public WebReviewPipeline(
        IGitHubPrFetcher prFetcher,
        IRepoCloner repoCloner,
        ICompilationLoader loader,
        IStreamingPersonaRunner personaRunner,
        IArbiter arbiter,
        ITurtleShellGuard guard,
        RubricLoader rubricLoader,
        IReviewJobStore jobStore,
        CouncilOptions councilOptions,
        string rubricPath = "turtle/rubric_v1.md")
    {
        _prFetcher = prFetcher ?? throw new ArgumentNullException(nameof(prFetcher));
        _repoCloner = repoCloner ?? throw new ArgumentNullException(nameof(repoCloner));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _personaRunner = personaRunner ?? throw new ArgumentNullException(nameof(personaRunner));
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _rubricLoader = rubricLoader ?? throw new ArgumentNullException(nameof(rubricLoader));
        _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
        _councilOptions = councilOptions ?? throw new ArgumentNullException(nameof(councilOptions));
        _rubricPath = rubricPath ?? throw new ArgumentNullException(nameof(rubricPath));
    }

    public async Task ExecuteAsync(Guid jobId, CancellationToken ct = default)
    {
        if (!_jobStore.TryGetJob(jobId, out var context) || context is null)
        {
            return;
        }

        var writer = _jobStore.GetWriter(jobId);

        try
        {
            // 1. Write SSE phase: validating
            await writer.WriteAsync(SseEvent.Phase("validating", "Validating GitHub pull request..."), ct).ConfigureAwait(false);

            // 2. Fetch PR info via GitHubPrFetcher
            if (!PrUrlParser.TryParse(context.PrUrl, out var parsedPr) || parsedPr is null)
            {
                var err = SseEvent.Error("INVALID_URL", "Invalid GitHub Pull Request URL.");
                await writer.WriteAsync(err, ct).ConfigureAwait(false);
                _jobStore.SetError(jobId, "Invalid GitHub Pull Request URL.");
                writer.TryComplete();
                return;
            }

            var prDetails = await _prFetcher.FetchAsync(parsedPr.Owner, parsedPr.Repo, parsedPr.PullNumber, ct).ConfigureAwait(false);

            // 3. If not accessible: Write SSE error (private/invalid) and exit
            if (!prDetails.IsAccessible)
            {
                var err = SseEvent.Error("NOT_ACCESSIBLE", "Pull request is private, invalid, or inaccessible.", "We tried peeking under the shell, but this repository is private or does not exist!");
                await writer.WriteAsync(err, ct).ConfigureAwait(false);
                _jobStore.SetError(jobId, "Pull request is private, invalid, or inaccessible.");
                writer.TryComplete();
                return;
            }

            // 4. If not .NET: Write SSE error (NOT_DOTNET with funny message) and exit
            if (!prDetails.IsDotNet)
            {
                var err = SseEvent.Error("NOT_DOTNET", "Non-.NET pull request detected.", prDetails.LanguageMessage);
                await writer.WriteAsync(err, ct).ConfigureAwait(false);
                _jobStore.SetError(jobId, prDetails.LanguageMessage);
                writer.TryComplete();
                return;
            }

            // 5. Write SSE phase: cloning
            await writer.WriteAsync(SseEvent.Phase("cloning", $"Cloning {parsedPr.Owner}/{parsedPr.Repo} (shallow)...", progress: 0.2), ct).ConfigureAwait(false);

            // 6. Clone via RepoCloner
            var cloneUrl = !string.IsNullOrWhiteSpace(prDetails.CloneUrl)
                ? prDetails.CloneUrl
                : $"https://github.com/{parsedPr.Owner}/{parsedPr.Repo}.git";

            string clonedDir = await _repoCloner.CloneAsync(cloneUrl, prDetails.HeadBranch, ct: ct).ConfigureAwait(false);

            try
            {
                // 7. Write SSE phase: compiling
                await writer.WriteAsync(SseEvent.Phase("compiling", "Compiling Roslyn workspace...", progress: 0.5), ct).ConfigureAwait(false);

                // 8. Compile via ICompilationLoader
                var projectOrSln = FindProjectOrSolution(clonedDir);
                if (projectOrSln is null)
                {
                    var err = SseEvent.Error("NO_PROJECT", "No .sln or .csproj found in repository.");
                    await writer.WriteAsync(err, ct).ConfigureAwait(false);
                    _jobStore.SetError(jobId, "No .sln or .csproj found in repository.");
                    writer.TryComplete();
                    return;
                }

                var loaded = await _loader.LoadAsync(projectOrSln, ct).ConfigureAwait(false);
                using (loaded.Workspace)
                {
                    // 9. Build PayloadGenerator with GitHubPrDiffProvider
                    var diffProvider = new GitHubPrDiffProvider(prDetails.ChangedFiles);
                    var changed = diffProvider.GetChangedCSharpFiles(clonedDir, prDetails.BaseSha);
                    var repoSlug = $"{parsedPr.Owner}/{parsedPr.Repo}";
                    var payload = new PayloadGenerator(loaded.Compilation).Build(repoSlug, prDetails.BaseSha ?? "main", changed);
                    var rubric = _rubricLoader.Load(ResolveRubric());
                    var promptPayload = PayloadTrimmer.TrimForPrompt(payload, _councilOptions);

                    // 10 & 11. Run streaming personas with start/finding/complete/fail SSE events
                    var verdicts = await _personaRunner.RunWithCallbacksAsync(
                        promptPayload,
                        rubric,
                        onStarted: async role =>
                        {
                            await writer.WriteAsync(SseEvent.PersonaStarted(role.ToString(), GetCharacterName(role)), ct).ConfigureAwait(false);
                        },
                        onCompleted: async verdict =>
                        {
                            foreach (var finding in verdict.Findings)
                            {
                                await writer.WriteAsync(SseEvent.PersonaFinding(verdict.Persona.ToString(), GetCharacterName(verdict.Persona), finding), ct).ConfigureAwait(false);
                            }
                            var quip = GetCompletedQuip(verdict.Persona, verdict.Findings.Count);
                            await writer.WriteAsync(SseEvent.PersonaCompleted(verdict.Persona.ToString(), GetCharacterName(verdict.Persona), verdict.Findings.Count, quip), ct).ConfigureAwait(false);
                        },
                        onFailed: async (role, message) =>
                        {
                            await writer.WriteAsync(SseEvent.PersonaFailed(role.ToString(), GetCharacterName(role), message), ct).ConfigureAwait(false);
                        },
                        ct: ct).ConfigureAwait(false);

                    // 12. Write SSE arbiter_merge
                    var merged = _arbiter.Merge(verdicts);
                    var totalRaw = verdicts.Sum(v => v.Findings.Count);
                    var dedupeCount = totalRaw - merged.Count;
                    var arbiterQuip = dedupeCount > 0
                        ? $"Order! {dedupeCount} duplicate finding(s) merged."
                        : "All persona findings reviewed and merged.";
                    await writer.WriteAsync(SseEvent.ArbiterMerge("Judge Shellsworth", totalRaw, merged.Count, arbiterQuip), ct).ConfigureAwait(false);

                    // 13. Write SSE guard_audit
                    var guarded = _guard.Verify(merged, payload);
                    var verified = guarded.Audit.Count(a => a.Verified);
                    var stripped = guarded.Audit.Count(a => !a.Verified);
                    await writer.WriteAsync(SseEvent.GuardAudit("Judge Shellsworth", verified, stripped, guarded.Audit.Count, guarded.Audit), ct).ConfigureAwait(false);

                    // 14. Write SSE completed
                    var markdown = _arbiter.RenderMarkdown(guarded.KeptFindings, payload);
                    const int ExpectedPersonas = 3;
                    if (verdicts.Count < ExpectedPersonas)
                    {
                        markdown = $"> Degraded review: {verdicts.Count}/{ExpectedPersonas} council personas succeeded.\n\n" + markdown;
                    }
                    var finalVerdict = new CouncilVerdict(verdicts, guarded.KeptFindings, guarded.Audit);
                    var stats = new
                    {
                        personasSucceeded = verdicts.Count,
                        findingsKept = guarded.KeptFindings.Count,
                        findingsStripped = stripped
                    };
                    await writer.WriteAsync(SseEvent.Completed(markdown, finalVerdict, stats), ct).ConfigureAwait(false);

                    // 15. Store result in ReviewJobStore
                    var reviewResult = new ReviewResult(markdown, finalVerdict, clonedDir);
                    _jobStore.SetResult(jobId, reviewResult);
                }
            }
            finally
            {
                _repoCloner.Cleanup(clonedDir);
                writer.TryComplete();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            var err = SseEvent.Error("PIPELINE_ERROR", ex.Message);
            await writer.WriteAsync(err, CancellationToken.None).ConfigureAwait(false);
            _jobStore.SetError(jobId, ex.Message);
            writer.TryComplete();
        }
    }

    private string ResolveRubric()
    {
        if (File.Exists(_rubricPath)) return _rubricPath;
        var candidate = Path.Combine(AppContext.BaseDirectory, _rubricPath);
        if (File.Exists(candidate)) return candidate;
        candidate = Path.Combine(Directory.GetCurrentDirectory(), _rubricPath);
        if (File.Exists(candidate)) return candidate;
        return _rubricPath;
    }

    private static string? FindProjectOrSolution(string clonedDir)
    {
        if (!Directory.Exists(clonedDir)) return null;
        return Directory.GetFiles(clonedDir, "*.sln").FirstOrDefault()
            ?? Directory.GetFiles(clonedDir, "*.csproj").FirstOrDefault()
            ?? Directory.GetFiles(clonedDir, "*.sln", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.GetFiles(clonedDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string GetCharacterName(PersonaRole role) => role switch
    {
        PersonaRole.AllocationsPerformance => "Speedy",
        PersonaRole.SecurityAuditor => "Sheldon",
        PersonaRole.IdiomaticArchitect => "Sensei",
        PersonaRole.Arbiter => "Judge Shellsworth",
        _ => "Turtle"
    };

    private static string GetCompletedQuip(PersonaRole role, int findingCount) => role switch
    {
        PersonaRole.AllocationsPerformance => $"{findingCount} allocation(s) found! My stopwatch is crying.",
        PersonaRole.SecurityAuditor => $"{findingCount} security consideration(s) detected.",
        PersonaRole.IdiomaticArchitect => $"{findingCount} architectural observation(s) noted.",
        _ => $"{findingCount} findings identified."
    };
}
