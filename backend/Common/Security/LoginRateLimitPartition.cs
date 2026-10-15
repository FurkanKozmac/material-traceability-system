using System.Threading.RateLimiting;

namespace backend.Common.Security;

public static class LoginRateLimitPartition
{
    public const int DefaultPermitLimit = 10;
    public const int DefaultWindowSeconds = 60;

    public static RateLimitPartition<string> Create(HttpContext context, int permitLimit, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
