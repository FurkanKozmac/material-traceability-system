using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public static class RefreshTokenCleaner
{
    public const int DefaultRetentionDays = 1;
    public const int DefaultBatchSize = 500;

    public static async Task<int> CleanupAsync(
        MtsDbContext context,
        DateTime now,
        int retentionDays = DefaultRetentionDays,
        int batchSize = DefaultBatchSize,
        CancellationToken ct = default)
    {
        if (retentionDays < 0)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days cannot be negative.");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

        var cutoff = now.ToUniversalTime().AddDays(-retentionDays);
        var totalDeleted = 0;

        while (true)
        {
            var batchIds = await context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.ExpiresAt < cutoff)
                .OrderBy(token => token.Id)
                .Take(batchSize)
                .Select(token => token.Id)
                .ToListAsync(ct);

            if (batchIds.Count == 0)
                break;

            var deleted = await context.RefreshTokens
                .Where(token => batchIds.Contains(token.Id))
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
            if (batchIds.Count < batchSize || deleted == 0)
                break;
        }

        return totalDeleted;
    }
}
