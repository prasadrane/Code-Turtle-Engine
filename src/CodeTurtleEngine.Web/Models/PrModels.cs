using System.Text.RegularExpressions;

namespace CodeTurtleEngine.Web.Models;

public record GitHubPrRequest(string PrUrl);

public record ParsedPrUrl(string Owner, string Repo, int PullNumber);

public static partial class PrUrlParser
{
    [GeneratedRegex(@"^https?:\/\/(?:www\.)?github\.com\/(?<owner>[a-zA-Z0-9_\-\.]+)\/(?<repo>[a-zA-Z0-9_\-\.]+)\/pull\/(?<number>\d+)(?:[\/?#].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrUrlRegex();

    public static bool TryParse(string? url, out ParsedPrUrl? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = PrUrlRegex().Match(url.Trim());
        if (!match.Success)
        {
            return false;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        if (!int.TryParse(match.Groups["number"].Value, out var pullNumber) || pullNumber <= 0)
        {
            return false;
        }

        result = new ParsedPrUrl(owner, repo, pullNumber);
        return true;
    }
}

public static class LanguageDetector
{
    private static readonly HashSet<string> DotNetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".fsproj"
    };

    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "Python",
        [".pyw"] = "Python",
        [".ipynb"] = "Python",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".mts"] = "TypeScript",
        [".cts"] = "TypeScript",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".mjs"] = "JavaScript",
        [".cjs"] = "JavaScript",
        [".rs"] = "Rust",
        [".go"] = "Go",
        [".java"] = "Java",
        [".jar"] = "Java"
    };

    private static readonly Dictionary<string, string> FunnyMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JavaScript"] = "JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!",
        ["TypeScript"] = "JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!",
        ["Python"] = "Indentation-based languages make our turtles dizzy. We only speak curly braces — C# curly braces.",
        ["Rust"] = "Your borrow checker is impressive, but our turtles haven't learned ownership yet. C# only for now!",
        ["Go"] = "`if err != nil` — we felt that. But our turtles only review C# for now!",
        ["Java"] = "So close! Same family, different shell. C# only for now!",
        ["Other"] = "Interesting language! Our turtles are still in C# school. More languages coming soon!"
    };

    public static (bool IsDotNet, string PrimaryLanguage, string FunnyMessage) Inspect(IEnumerable<string>? filePaths)
    {
        if (filePaths == null)
        {
            return (false, "Other", FunnyMessages["Other"]);
        }

        var paths = filePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count == 0)
        {
            return (false, "Other", FunnyMessages["Other"]);
        }

        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path);
            if (DotNetExtensions.Contains(ext))
            {
                return (true, "C#", string.Empty);
            }
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path);
            if (ExtensionToLanguage.TryGetValue(ext, out var lang))
            {
                counts[lang] = counts.GetValueOrDefault(lang) + 1;
            }
        }

        if (counts.Count == 0)
        {
            return (false, "Other", FunnyMessages["Other"]);
        }

        var primary = counts.OrderByDescending(kvp => kvp.Value).First().Key;
        var message = FunnyMessages.GetValueOrDefault(primary, FunnyMessages["Other"]);

        return (false, primary, message);
    }
}
