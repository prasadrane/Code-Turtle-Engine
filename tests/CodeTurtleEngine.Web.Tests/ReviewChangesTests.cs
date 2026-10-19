using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using LibGit2Sharp;

namespace CodeTurtleEngine.Web.Tests;

public sealed class ReviewChangesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "turtle-diff-" + Guid.NewGuid().ToString("N"));
    private readonly Repository _repo;
    private readonly Signature _author = new("Fixture", "fixture@example.invalid", DateTimeOffset.UnixEpoch);

    public ReviewChangesTests()
    {
        Repository.Init(_root);
        _repo = new Repository(_root);
    }

    private Commit Commit(string message)
    {
        Commands.Stage(_repo, "*");
        return _repo.Commit(message, _author, _author);
    }

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(_root, path), text);
    private static GitHubPrDetails Details(Commit baseline, Commit head) => new(true, true,
        baseline.Sha, head.Sha, "feature", ["Service.cs"], "", Title: "Review this change");
    private static RoslynPayload Payload(params FileFacts[] files) => new("fixture/repo", "base", files);

    [Fact]
    public void UsesMergeBaseRatherThanAdvancedBaseTip_AndPinsHeadSource()
    {
        Write("Service.cs", "class Service\n{\n    int Read() => 1;\n}\n");
        var common = Commit("common");
        var feature = _repo.CreateBranch("feature");
        Write("BaseOnly.cs", "class BaseOnly {}\n");
        var baseTip = Commit("base advanced");
        Commands.Checkout(_repo, feature);
        Write("Service.cs", "class Service\n{\n    // context\n    int Read() => 2;\n}\n");
        var head = Commit("head");

        var result = new ReviewChangesBuilder().Build(_root, "fixture/repo", 1, Details(baseTip, head), Payload());

        Assert.Equal(common.Sha, result.Snapshot.MergeBaseSha);
        Assert.Equal(head.Sha, result.Snapshot.HeadSha);
        Assert.Equal("merge-base-to-head", result.Snapshot.ComparisonKind);
        Assert.Single(result.Files);
        var file = result.Files[0];
        Assert.Contains("Read() => 2", file.Source);
        var added = file.Hunks.SelectMany(h => h.Lines).Single(l => l.Kind == "added" && l.Text.Contains("Read"));
        Assert.Equal(4, added.NewLine);
        Assert.Null(added.OldLine);
        Assert.DoesNotContain(result.Files, f => f.Path == "BaseOnly.cs");
    }

    [Fact]
    public void RepresentsAddedDeletedRenamedAndUnreviewedFiles()
    {
        Write("Service.cs", "class Service {}\n");
        Write("Deleted.cs", "class Deleted {}\n");
        var baseline = Commit("base");
        File.Move(Path.Combine(_root, "Service.cs"), Path.Combine(_root, "Renamed.cs"));
        File.Delete(Path.Combine(_root, "Deleted.cs"));
        Write("Added.cs", "class Added {}\n");
        Write("note.txt", "outside the C# review\n");
        var head = Commit("changes");
        var result = new ReviewChangesBuilder().Build(_root, "fixture/repo", 1, Details(baseline, head), Payload());
        Assert.Contains(result.Files, f => f.Path == "Renamed.cs" && f.OldPath == "Service.cs" && f.Status == "Renamed");
        Assert.Contains(result.Files, f => f.Path == "Deleted.cs" && f.Status == "Deleted" && f.Source == null);
        Assert.Contains(result.Files, f => f.Path == "Added.cs" && f.Status == "Added");
        Assert.All(result.Files, f => Assert.False(f.Analyzed));
        Assert.Contains(result.Files, f => f.Path == "note.txt" && f.UnavailableReason != null);
    }

    [Fact]
    public void UnavailableBaseStillProvidesPinnedSource_WithoutInventingDiff()
    {
        Write("Service.cs", "class Service {}\n");
        var head = Commit("head");
        var details = Details(head, head) with { BaseSha = new string('a', 40) };
        var result = new ReviewChangesBuilder().Build(_root, "fixture/repo", 1, details, Payload());
        Assert.Equal("head-only", result.Snapshot.ComparisonKind);
        Assert.NotNull(result.UnavailableReason);
        Assert.Single(result.Files);
        Assert.Empty(result.Files[0].Hunks);
        Assert.NotNull(result.Files[0].Source);
    }

    [Fact]
    public void RejectsHeadMismatch_AndOmitsOversizedOrNonTextContent()
    {
        Write("Service.cs", "class Service {}\n");
        var baseline = Commit("base");
        Write("Service.cs", new string('x', ReviewChangesBuilder.MaxFileBytes + 1));
        Write("Binary.cs", "hello\0world");
        var head = Commit("large and binary");
        var builder = new ReviewChangesBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build(_root, "fixture/repo", 1,
            Details(baseline, head) with { HeadSha = baseline.Sha }, Payload()));
        var result = builder.Build(_root, "fixture/repo", 1, Details(baseline, head), Payload());
        Assert.All(result.Files, f => { Assert.Null(f.Source); Assert.Empty(f.Hunks); Assert.NotNull(f.UnavailableReason); });
    }

    [Fact]
    public void AnchorsOnlyUniqueDeclaredMembers_NeverModelLocationOrCalledSymbol()
    {
        Write("Service.cs", "class Service\n{\n    int Read() => 1;\n}\n");
        var baseline = Commit("base");
        Write("Service.cs", "class Service\n{\n    int Read() => 2;\n}\n");
        var head = Commit("head");
        var method = new MethodFacts("Service.Read", [], null, [], ["Service.Read", "Other.Called"], 3, 3);
        var payload = Payload(new FileFacts("Service.cs", [method]));
        var builder = new ReviewChangesBuilder();
        var changes = builder.Build(_root, "fixture/repo", 1, Details(baseline, head), payload);
        ReviewFinding Finding(params string[] symbols) => new(PersonaRole.SecurityAuditor, Severity.Warning,
            "Check this", "Recommendation", "Untrusted.cs:999", symbols);
        var anchored = FindingAnchors.Attach(changes, payload, [Finding("Service.Read"), Finding("Other.Called"), Finding()]);
        var anchor = Assert.Single(anchored.Anchors);
        Assert.Equal(0, anchor.FindingIndex);
        Assert.Equal(3, anchor.StartLine);
        Assert.Equal("RoslynMember", anchor.Provenance);
        var ambiguous = Payload(new FileFacts("Service.cs", [method, method with { StartLine = 4, EndLine = 4 }]));
        Assert.Empty(FindingAnchors.Attach(changes, ambiguous, [Finding("Service.Read")]).Anchors);
    }

    [Fact]
    public void DisplayLimitPrioritizesAnalyzedCSharpOverAlphabeticallyEarlierDocuments()
    {
        Write("Service.cs", "class Service {}\n");
        var baseline = Commit("base");
        Write("Service.cs", "class Service { int Read() => 1; }\n");
        for (var index = 0; index < ReviewChangesBuilder.MaxFiles; index++) Write($"A{index:000}.txt", "metadata only\n");
        var head = Commit("many docs and code");
        var result = new ReviewChangesBuilder().Build(_root, "fixture/repo", 1, Details(baseline, head),
            Payload(new FileFacts("Service.cs", [])));
        Assert.Equal("Service.cs", result.Files[0].Path);
        Assert.Equal(ReviewChangesBuilder.MaxFiles, result.Files.Count);
        Assert.Contains("additional files omitted", result.UnavailableReason);
    }

    public void Dispose()
    {
        _repo.Dispose();
        new RepoCloner().Cleanup(_root);
    }
}
