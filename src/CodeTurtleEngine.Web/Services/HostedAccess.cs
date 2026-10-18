using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace CodeTurtleEngine.Web.Services;

public sealed class HostedAccessOptions
{
    public string Mode { get; set; } = "Disabled";
    public string ApiKey { get; set; } = "";
    public string[] Origins { get; set; } = [];
    public string[] TrustedRepositories { get; set; } = [];
    public int RequestsPerMinute { get; set; } = 30;
}

public static class HostedAccess
{
    public static void AddReviewAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ReviewSessions>();
        services.AddSingleton(_ =>
        {
            var options = configuration.GetSection("Access").Get<HostedAccessOptions>() ?? new();
            if (options.RequestsPerMinute is < 1 or > 1000) throw new ArgumentException("Invalid request limit.");
            return options;
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("review", context => RateLimitPartition.GetFixedWindowLimiter("beta", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = context.RequestServices.GetRequiredService<HostedAccessOptions>().RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public static async Task Enforce(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var options = context.RequestServices.GetRequiredService<HostedAccessOptions>();
        // Hosted acquisition/isolation rollout is a separate gate; no host fallback.
        if (options.Mode != "TrustedLocal" || string.IsNullOrWhiteSpace(options.ApiKey))
        { context.Response.StatusCode = 503; return; }
        if (context.Connection.RemoteIpAddress is { } peer && !IPAddress.IsLoopback(peer))
        { context.Response.StatusCode = 403; return; }
        var supplied = context.Request.Headers.Authorization.ToString();
        var expected = "Bearer " + options.ApiKey;
        var bearerValid = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
        var sessionValid = context.RequestServices.GetRequiredService<ReviewSessions>().Accepts(context.Request.Cookies["turtle-session"]);
        if (!bearerValid && (!sessionValid || context.Request.Path == "/api/session"))
        { context.Response.StatusCode = 401; return; }
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !options.Origins.Contains(origin, StringComparer.Ordinal))
        { context.Response.StatusCode = 403; return; }
        await next(context);
    }
}
