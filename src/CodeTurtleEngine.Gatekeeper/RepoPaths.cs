namespace CodeTurtleEngine.Gatekeeper;

public static class RepoPaths
{
    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="relative"/> or ends with it at a
    /// path-segment boundary. Separator- and case-insensitive. "PaymentService.cs" does not
    /// match "Service.cs".
    /// </summary>
    public static bool EndsWith(string path, string relative)
    {
        var a = path.Replace('\\', '/');
        var b = relative.Replace('\\', '/');
        if (b.StartsWith("./", StringComparison.Ordinal)) b = b[2..];
        return a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.EndsWith("/" + b, StringComparison.OrdinalIgnoreCase);
    }
}
