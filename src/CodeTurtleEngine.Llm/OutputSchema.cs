using System.Text.Json;

namespace CodeTurtleEngine.Llm;

// Validator for the schema vocabulary used by review prompts. Unsupported keywords fail
// at configuration time rather than silently weakening the declared contract.
internal static class OutputSchema
{
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
        { "type", "properties", "required", "additionalProperties", "items", "enum", "description", "title", "$schema" };

    public static void Check(JsonElement schema)
    {
        foreach (var property in schema.EnumerateObject())
            if (!Supported.Contains(property.Name))
                throw new ArgumentException($"Unsupported output schema keyword: {property.Name}");
        if (schema.TryGetProperty("properties", out var properties))
            foreach (var property in properties.EnumerateObject()) Check(property.Value);
        if (schema.TryGetProperty("items", out var items)) Check(items);
        if (schema.TryGetProperty("additionalProperties", out var extra) && extra.ValueKind == JsonValueKind.Object) Check(extra);
    }

    public static void Validate(JsonElement value, JsonElement schema, string path = "$")
    {
        if (schema.TryGetProperty("type", out var type))
        {
            var matches = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Any(t => Matches(value, t.GetString()))
                : Matches(value, type.GetString());
            if (!matches) throw new ModelException($"Schema-invalid model output at {path}: expected {type}.");
        }
        if (schema.TryGetProperty("enum", out var choices) &&
            !choices.EnumerateArray().Any(c => JsonElement.DeepEquals(c, value)))
            throw new ModelException($"Schema-invalid enum at {path}.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray())
                    if (!value.TryGetProperty(name.GetString()!, out _))
                        throw new ModelException($"Missing required model output property {path}.{name.GetString()}.");
            schema.TryGetProperty("properties", out var properties);
            schema.TryGetProperty("additionalProperties", out var additional);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new ModelException($"Duplicate property at {path}.{property.Name}.");
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var child))
                    Validate(property.Value, child, $"{path}.{property.Name}");
                else if (additional.ValueKind == JsonValueKind.False)
                    throw new ModelException($"Unexpected model output property {path}.{property.Name}.");
                else if (additional.ValueKind == JsonValueKind.Object)
                    Validate(property.Value, additional, $"{path}.{property.Name}");
            }
        }
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) Validate(item, items, $"{path}[{index++}]");
        }
    }

    private static bool Matches(JsonElement value, string? type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) && decimal.Truncate(n) == n,
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new ArgumentException($"Unsupported schema type: {type}")
    };
}
