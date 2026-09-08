using CodeTurtleEngine.Core;
using System.Globalization;
using System.Text.Json;

namespace CodeTurtleEngine.Cli;

public sealed class ArtifactWriter
{
    private readonly string _root;

    public ArtifactWriter(string root) => _root = root;

    public string Write(string repoSlug, RoslynPayload payload, CouncilVerdict verdict, string markdown)
    {
        var dir = Path.Combine(_root, "reviews", Slugify(repoSlug), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture));
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
