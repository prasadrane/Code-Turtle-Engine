namespace CodeTurtleEngine.Gatekeeper;

public sealed class CompilationException : Exception
{
    public CompilationException(string message, Exception? inner = null) : base(message, inner) { }
}
