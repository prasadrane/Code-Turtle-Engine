namespace CodeTurtleEngine.Llm;

public sealed class RouteOptions
{
    public string Name { get; set; } = "";
    public string BaseUrlEnv { get; set; } = "";
    public string ApiKeyEnv { get; set; } = "";
    public string Model { get; set; } = "";
    /// <summary>Wire protocol for this route: "openai" (chat/completions) or "anthropic" (Messages).</summary>
    public string Protocol { get; set; } = "openai";
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public List<RouteOptions> Routes { get; set; } = new();
    public Dictionary<string, string> ModelRoles { get; set; } = new();
    /// <summary>Extra re-rolls of a structured call when the model returns malformed/invalid JSON.</summary>
    public int JsonRetries { get; set; } = 1;
}
