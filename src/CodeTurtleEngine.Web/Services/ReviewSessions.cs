using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CodeTurtleEngine.Web.Services;

// Single-team local beta, not multi-tenant identity. Tokens never enter URLs or workers.
public sealed class ReviewSessions
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sessions = new();
    public string Issue()
    {
        foreach (var (key, expiry) in _sessions)
            if (expiry <= DateTimeOffset.UtcNow) _sessions.TryRemove(key, out _);
        if (_sessions.Count >= 128) throw new InvalidOperationException("Session capacity reached.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[Hash(token)] = DateTimeOffset.UtcNow.AddMinutes(15);
        return token;
    }
    public bool Accepts(string? token) => token is { Length: 64 } &&
        _sessions.TryGetValue(Hash(token), out var expiry) && expiry > DateTimeOffset.UtcNow;
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
