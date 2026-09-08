namespace CodeTurtleEngine.Llm;

public enum LlmErrorKind { None, Auth, RateLimit, ServerError, Timeout, BadOutput, Unknown }

public static class ErrorClassifier
{
    public static LlmErrorKind ClassifyStatus(int httpStatus, string? errorBody = null)
    {
        if (httpStatus is 401 or 403) return LlmErrorKind.Auth;
        if (httpStatus == 429) return LlmErrorKind.RateLimit;
        if (httpStatus == 408) return LlmErrorKind.Timeout;
        if (httpStatus >= 500 && httpStatus <= 599) return LlmErrorKind.ServerError;
        if (httpStatus >= 200 && httpStatus < 300)
            return string.IsNullOrWhiteSpace(errorBody) ? LlmErrorKind.None : LlmErrorKind.BadOutput;
        return LlmErrorKind.Unknown;
    }
}
