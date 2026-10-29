using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Cli;

public sealed class TurtleOptions
{
    public const string SectionName = "Turtle";
    public string RubricPath { get; set; } = "turtle/rubric_v1.md";
    public string? SarifPath { get; set; }
    public CouncilOptions Council { get; set; } = new();
}
