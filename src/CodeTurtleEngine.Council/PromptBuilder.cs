using CodeTurtleEngine.Core;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeTurtleEngine.Council;

public static class PromptBuilder
{
    public static string VerdictSchema { get; } = """
    {"type":"object","additionalProperties":false,"properties":{
      "Findings":{"type":"array","items":{
        "type":"object","additionalProperties":false,"properties":{
          "Severity":{"type":"string","enum":["Info","Nit","Warning","Error","Critical"]},
          "Title":{"type":"string"},
          "Detail":{"type":"string"},
          "Location":{"type":"string"},
          "CitedSymbolFqns":{"type":"array","items":{"type":"string"}}
        },"required":["Severity","Title","Detail","Location","CitedSymbolFqns"]}}},
      "required":["Findings"]}
    """;

    public static IReadOnlyList<ChatMessage> Build(PersonaRole role, RoslynPayload payload, string rubric)
    {
        var system = $"{PersonaInstruction(role)}\n\n# Review Rubric\n{rubric}\n\n{GroundingRules}";
        var user = JsonSerializer.Serialize(payload, TurtleJson.Options);
        return new[]
        {
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user)
        };
    }

    private const string GroundingRules =
        "You are given a minified JSON semantic payload extracted by Roslyn. " +
        "Do NOT invent symbols. Every finding MUST cite symbol FQNs that appear in the payload's ResolvedSymbols. " +
        "Return ONLY JSON matching the schema.";

    private static string PersonaInstruction(PersonaRole role) => role switch
    {
        PersonaRole.AllocationsPerformance =>
            "You are a C# allocations and performance reviewer. Scrutinize memory leaks, large-object-heap (LOH) risk, unawaited tasks, and thread-safety issues in concurrent collections.",
        PersonaRole.SecurityAuditor =>
            "You are a C# security auditor. Evaluate input sanitization, SQL-injection vectors (raw string concatenation bypassing EF Core parameters), and authentication-bypass risks on REST/GraphQL endpoints.",
        PersonaRole.IdiomaticArchitect =>
            "You are an idiomatic C# architect. Enforce modern C# language features, clean-architecture boundaries, and correct Dependency Injection lifecycles (e.g., capturing Transient services inside Singleton classes).",
        _ => "You are a C# code reviewer."
    };
}
