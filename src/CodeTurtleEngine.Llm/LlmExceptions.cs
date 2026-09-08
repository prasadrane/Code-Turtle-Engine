namespace CodeTurtleEngine.Llm;

public sealed class ProviderException : Exception
{
    public IReadOnlyList<string> AttemptLog { get; }

    public ProviderException(string message, IReadOnlyList<string> attemptLog, Exception? inner = null)
        : base(message, inner) => AttemptLog = attemptLog;
}

public sealed class ModelException : Exception
{
    public ModelException(string message, Exception? inner = null) : base(message, inner) { }
}
