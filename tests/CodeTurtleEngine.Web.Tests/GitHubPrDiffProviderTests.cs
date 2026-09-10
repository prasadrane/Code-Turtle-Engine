using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Services;

namespace CodeTurtleEngine.Web.Tests;

public class GitHubPrDiffProviderTests
{
    [Fact]
    public void Implements_IDiffProvider_Interface()
    {
        var provider = new GitHubPrDiffProvider(Array.Empty<string>());
        Assert.IsAssignableFrom<IDiffProvider>(provider);
    }

    [Fact]
    public void GetChangedCSharpFiles_MixedFiles_FiltersOnlyCSharpFiles()
    {
        var input = new[]
        {
            "src/App/Program.cs",
            "src/App/App.csproj",
            "README.md",
            "frontend/app.ts",
            "scripts/deploy.py",
            "src/Domain/Entity.cs"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("dummyRepo", "main");

        Assert.Equal(new[] { "src/App/Program.cs", "src/Domain/Entity.cs" }, result);
    }

    [Fact]
    public void GetChangedCSharpFiles_CaseInsensitiveExtension_IncludesAllVariations()
    {
        var input = new[]
        {
            "src/File1.cs",
            "src/File2.CS",
            "src/File3.Cs",
            "src/File4.cS"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("anyRepo", null);

        Assert.Equal(4, result.Count);
        Assert.Contains("src/File1.cs", result);
        Assert.Contains("src/File2.CS", result);
        Assert.Contains("src/File3.Cs", result);
        Assert.Contains("src/File4.cS", result);
    }

    [Fact]
    public void GetChangedCSharpFiles_NonMatchingExtensions_Excluded()
    {
        var input = new[]
        {
            "src/File.cs.bak",
            "src/File.csproj",
            "src/File.cs_old",
            "src/cs",
            "src/cs/readme.txt"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("/some/path", null);

        Assert.Empty(result);
    }

    [Fact]
    public void GetChangedCSharpFiles_DuplicatePaths_DeduplicatesCaseInsensitively()
    {
        var input = new[]
        {
            "src/Services/DiffProvider.cs",
            "src/services/diffprovider.cs",
            "SRC/SERVICES/DIFFPROVIDER.CS",
            "src/Models/Item.cs"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("", null);

        Assert.Equal(2, result.Count);
        Assert.Equal("src/Services/DiffProvider.cs", result[0]);
        Assert.Equal("src/Models/Item.cs", result[1]);
    }

    [Fact]
    public void GetChangedCSharpFiles_PathSeparators_NormalizesBackslashesToForwardSlashes()
    {
        var input = new[]
        {
            @"src\Services\DiffService.cs",
            @"tests\Unit\DiffTests.cs"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("repo", "ref");

        Assert.Equal(new[] { "src/Services/DiffService.cs", "tests/Unit/DiffTests.cs" }, result);
    }

    [Fact]
    public void GetChangedCSharpFiles_DuplicateAfterNormalization_Deduplicates()
    {
        var input = new[]
        {
            "src/Services/DiffProvider.cs",
            @"src\Services\DiffProvider.cs",
            @"src\services\diffprovider.cs"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("", null);

        Assert.Single(result);
        Assert.Equal("src/Services/DiffProvider.cs", result[0]);
    }

    [Fact]
    public void GetChangedCSharpFiles_NullOrEmptyCollection_ReturnsEmptyList()
    {
        var providerFromNull = new GitHubPrDiffProvider(null);
        var resultFromNull = providerFromNull.GetChangedCSharpFiles("repo", null);

        var providerFromEmpty = new GitHubPrDiffProvider(Array.Empty<string>());
        var resultFromEmpty = providerFromEmpty.GetChangedCSharpFiles("repo", null);

        Assert.NotNull(resultFromNull);
        Assert.Empty(resultFromNull);

        Assert.NotNull(resultFromEmpty);
        Assert.Empty(resultFromEmpty);
    }

    [Fact]
    public void GetChangedCSharpFiles_NullOrWhitespaceEntries_Ignored()
    {
        var input = new[]
        {
            null,
            "",
            "   \t\n",
            "src/Valid.cs"
        };

        var provider = new GitHubPrDiffProvider(input!);
        var result = provider.GetChangedCSharpFiles("repo", null);

        Assert.Single(result);
        Assert.Equal("src/Valid.cs", result[0]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("/var/repo", "origin/main")]
    [InlineData("C:\\git\\repo", "feature/abc")]
    public void GetChangedCSharpFiles_IgnoresRepoPathAndBaselineRef(string? repoPath, string? baselineRef)
    {
        var input = new[] { "src/Test.cs" };
        var provider = new GitHubPrDiffProvider(input);

        var result = provider.GetChangedCSharpFiles(repoPath!, baselineRef);

        Assert.Single(result);
        Assert.Equal("src/Test.cs", result[0]);
    }

    [Fact]
    public void GetChangedCSharpFiles_TrimsLeadingSlashOrDotSlash()
    {
        var input = new[]
        {
            "/src/RootSlash.cs",
            "\\src\\RootBackslash.cs",
            "./src/DotSlash.cs",
            ".\\src\\DotBackslash.cs"
        };

        var provider = new GitHubPrDiffProvider(input);
        var result = provider.GetChangedCSharpFiles("", null);

        Assert.Equal(4, result.Count);
        Assert.All(result, item => Assert.False(item.StartsWith('/') || item.StartsWith('\\') || item.StartsWith('.')));
        Assert.Contains("src/RootSlash.cs", result);
        Assert.Contains("src/RootBackslash.cs", result);
        Assert.Contains("src/DotSlash.cs", result);
        Assert.Contains("src/DotBackslash.cs", result);
    }
}
