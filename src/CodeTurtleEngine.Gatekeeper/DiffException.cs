namespace CodeTurtleEngine.Gatekeeper;

public sealed class DiffException : Exception
{
    public DiffException(string message, Exception? inner = null) : base(message, inner) { }
}
