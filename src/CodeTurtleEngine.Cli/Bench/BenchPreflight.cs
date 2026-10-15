using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Cli.Bench;

public static class BenchPreflight
{
    /// <summary>Fails fast before any compilation; names missing env variables, never values.</summary>
    public static void RequireCredentials(LlmOptions llm, Func<string, string?> getEnv)
    {
        if (llm.Routes.Count == 0) throw new BenchException("No LLM routes configured (Llm:Routes).");
        var missing = llm.Routes
            .SelectMany(r => new[] { r.BaseUrlEnv, r.ApiKeyEnv })
            .Where(name => !string.IsNullOrEmpty(name) && string.IsNullOrEmpty(getEnv(name)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (missing.Count > 0)
            throw new BenchException($"Missing LLM credentials: set {string.Join(", ", missing)}.");
    }
}
