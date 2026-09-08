using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeTurtleEngine.Core;

public static class TurtleJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
