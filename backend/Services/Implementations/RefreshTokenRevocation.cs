using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

internal static class RefreshTokenRevocation
{
    public static async Task RevokeAllActiveAsync(
        MtsDbContext context, long userId, CancellationToken ct = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;
    }
}
