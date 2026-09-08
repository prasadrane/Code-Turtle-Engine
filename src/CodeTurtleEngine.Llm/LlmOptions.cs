namespace CodeTurtleEngine.Llm;

public sealed class RouteOptions
{
    public string Name { get; set; } = "";
    public string BaseUrlEnv { get; set; } = "";
    public string ApiKeyEnv { get; set; } = "";
    public string Model { get; set; } = "";
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public List<RouteOptions> Routes { get; set; } = new();
    public Dictionary<string, string> ModelRoles { get; set; } = new();
}
