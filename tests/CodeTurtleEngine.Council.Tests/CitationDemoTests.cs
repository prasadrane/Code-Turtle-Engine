using System.Text.Json;
using CodeTurtleEngine.CitationDemo;

namespace CodeTurtleEngine.Council.Tests;

public class CitationDemoTests
{
    [Fact]
    public void Recruiter_Playback_Matches_Current_Compiler_And_Guard_Output()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CodeTurtleEngine.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var saved = File.ReadAllText(Path.Combine(directory.FullName, "web/src/data/citation-demo.json"));
        var actual = JsonSerializer.Serialize(DemoScenario.Run(), DemoScenario.JsonOptions) + "\n";
        Assert.Equal(saved.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
        var report = DemoScenario.Run();
        Assert.Equal(3, report.Outcome.KeptFindings.Count);
        Assert.Equal(2, report.Outcome.Audit.Count(a => !a.Verified));
    }
}
