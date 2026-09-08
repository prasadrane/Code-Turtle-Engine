using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Llm.Tests;

public class ErrorClassifierTests
{
    [Theory]
    [InlineData(401, null, LlmErrorKind.Auth)]
    [InlineData(403, null, LlmErrorKind.Auth)]
    [InlineData(429, null, LlmErrorKind.RateLimit)]
    [InlineData(500, null, LlmErrorKind.ServerError)]
    [InlineData(503, null, LlmErrorKind.ServerError)]
    [InlineData(408, null, LlmErrorKind.Timeout)]
    [InlineData(200, null, LlmErrorKind.None)]
    [InlineData(200, "model overloaded", LlmErrorKind.BadOutput)]
    [InlineData(418, null, LlmErrorKind.Unknown)]
    public void Classifies(int status, string? body, LlmErrorKind expected)
        => Assert.Equal(expected, ErrorClassifier.ClassifyStatus(status, body));
}
