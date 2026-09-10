using CodeTurtleEngine.Web.Models;

namespace CodeTurtleEngine.Web.Tests;

public class PrModelsTests
{
    [Fact]
    public void GitHubPrRequest_StoresPrUrl()
    {
        var request = new GitHubPrRequest("https://github.com/owner/repo/pull/123");
        Assert.Equal("https://github.com/owner/repo/pull/123", request.PrUrl);
    }

    [Fact]
    public void ParsedPrUrl_StoresPropertiesCorrectly()
    {
        var parsed = new ParsedPrUrl("owner", "repo", 42);
        Assert.Equal("owner", parsed.Owner);
        Assert.Equal("repo", parsed.Repo);
        Assert.Equal(42, parsed.PullNumber);
    }

    [Theory]
    [InlineData("https://github.com/octocat/Hello-World/pull/42", "octocat", "Hello-World", 42)]
    [InlineData("https://github.com/dotnet/runtime/pull/1000/", "dotnet", "runtime", 1000)]
    [InlineData("https://github.com/dotnet/aspnetcore/pull/500?diff=unified", "dotnet", "aspnetcore", 500)]
    [InlineData("https://github.com/owner/repo/pull/123#issuecomment-1", "owner", "repo", 123)]
    [InlineData("http://github.com/owner/repo/pull/789", "owner", "repo", 789)]
    [InlineData("https://www.github.com/owner/repo/pull/10", "owner", "repo", 10)]
    [InlineData("https://github.com/owner/repo/pull/999/files", "owner", "repo", 999)]
    [InlineData("  https://github.com/owner/repo/pull/55  ", "owner", "repo", 55)]
    public void PrUrlParser_TryParse_ValidUrls_ReturnsTrueAndCorrectParsedPrUrl(
        string url, string expectedOwner, string expectedRepo, int expectedPullNumber)
    {
        var success = PrUrlParser.TryParse(url, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(expectedOwner, result.Owner);
        Assert.Equal(expectedRepo, result.Repo);
        Assert.Equal(expectedPullNumber, result.PullNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://gitlab.com/owner/repo/pull/123")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/issues/123")]
    [InlineData("https://github.com/owner/repo/pull/")]
    [InlineData("https://github.com/owner/repo/pull/notanumber")]
    [InlineData("https://github.com/owner/repo/pull/-5")]
    [InlineData("https://github.com/owner/repo/pull/0")]
    public void PrUrlParser_TryParse_InvalidUrls_ReturnsFalseAndNullResult(string? url)
    {
        var success = PrUrlParser.TryParse(url!, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("src/Program.cs")]
    [InlineData("src/App.csproj")]
    [InlineData("CodeTurtleEngine.sln")]
    [InlineData("src/Project.fsproj")]
    [InlineData("SRC/PROGRAM.CS")]
    [InlineData("src/App.Csproj")]
    [InlineData("SOLUTION.SLN")]
    public void LanguageDetector_Inspect_DotNetFiles_ReturnsIsDotNetTrue(string filePath)
    {
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(new[] { filePath });

        Assert.True(isDotNet);
        Assert.Equal("C#", language);
        Assert.Empty(funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_MixedWithDotNetFile_ReturnsIsDotNetTrue()
    {
        var files = new[] { "README.md", "package.json", "src/Program.cs", "scripts/deploy.py" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.True(isDotNet);
        Assert.Equal("C#", language);
        Assert.Empty(funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_PythonFiles_ReturnsNonDotNetWithPythonMessage()
    {
        var files = new[] { "main.py", "utils.py", "setup.py" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("Python", language);
        Assert.Equal("Indentation-based languages make our turtles dizzy. We only speak curly braces — C# curly braces.", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_JavaScriptFiles_ReturnsNonDotNetWithJsMessage()
    {
        var files = new[] { "index.js", "src/app.jsx" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("JavaScript", language);
        Assert.Equal("JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_TypeScriptFiles_ReturnsNonDotNetWithTsMessage()
    {
        var files = new[] { "src/index.ts", "src/components/Button.tsx" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("TypeScript", language);
        Assert.Equal("JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_RustFiles_ReturnsNonDotNetWithRustMessage()
    {
        var files = new[] { "src/main.rs", "Cargo.toml" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("Rust", language);
        Assert.Equal("Your borrow checker is impressive, but our turtles haven't learned ownership yet. C# only for now!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_GoFiles_ReturnsNonDotNetWithGoMessage()
    {
        var files = new[] { "main.go", "internal/server.go" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("Go", language);
        Assert.Equal("`if err != nil` — we felt that. But our turtles only review C# for now!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_JavaFiles_ReturnsNonDotNetWithJavaMessage()
    {
        var files = new[] { "src/main/java/App.java" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("Java", language);
        Assert.Equal("So close! Same family, different shell. C# only for now!", funnyMessage);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/spec.txt")]
    [InlineData("style.css")]
    public void LanguageDetector_Inspect_OtherFiles_ReturnsOtherMessage(string file)
    {
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(new[] { file });

        Assert.False(isDotNet);
        Assert.Equal("Other", language);
        Assert.Equal("Interesting language! Our turtles are still in C# school. More languages coming soon!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_EmptyList_ReturnsOtherMessage()
    {
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(Array.Empty<string>());

        Assert.False(isDotNet);
        Assert.Equal("Other", language);
        Assert.Equal("Interesting language! Our turtles are still in C# school. More languages coming soon!", funnyMessage);
    }

    [Fact]
    public void LanguageDetector_Inspect_MixedNonDotNet_PicksMajorityLanguage()
    {
        var files = new[] { "main.py", "helper.py", "script.go" };
        var (isDotNet, language, funnyMessage) = LanguageDetector.Inspect(files);

        Assert.False(isDotNet);
        Assert.Equal("Python", language);
        Assert.Equal("Indentation-based languages make our turtles dizzy. We only speak curly braces — C# curly braces.", funnyMessage);
    }
}
