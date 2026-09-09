using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed class CouncilOptions
{
    public int Quorum { get; set; } = 2;
    public GuardMode Guard { get; set; } = GuardMode.Strip;

    // Prompt-payload trim caps: applied ONLY to the payload projection sent to the
    // personas. The Turtle Shell guard always verifies against the FULL payload.
    public int MaxSymbolsPerMethod { get; set; } = 20;
    public int MaxAllocationsPerMethod { get; set; } = 10;
    public int MaxMethodsPerFile { get; set; } = 40;
    public int MaxFiles { get; set; } = 40;
    public int MaxFindingsPerPersona { get; set; } = 5;
}
