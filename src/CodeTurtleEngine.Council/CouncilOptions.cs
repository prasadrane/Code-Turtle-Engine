using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed class CouncilOptions
{
    public int Quorum { get; set; } = 2;
    public GuardMode Guard { get; set; } = GuardMode.Strip;
}
