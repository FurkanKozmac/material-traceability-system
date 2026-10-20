using backend.Entities;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public class RefreshTokenCleanerTests
{
    [Fact]
    public async Task Cleanup_DeletesOnlyTokensExpiredBeyondRetention()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Username = "refresh-cleanup-user",
            Password = "not-used-in-this-test",
            Active = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var expiredOldFirst = AddToken(context, user.Id, "expired-old-1", now.AddDays(-3));
        var expiredOldSecond = AddToken(context, user.Id, "expired-old-2", now.AddDays(-2));
        var recentExpired = AddToken(context, user.Id, "recent-expired", now.AddHours(-12));
        var active = AddToken(context, user.Id, "active-token", now.AddDays(1));
        var revokedButValid = AddToken(
            context, user.Id, "revoked-but-valid", now.AddDays(1), now.AddHours(-1));
        await context.SaveChangesAsync();

        // Act
        var deleted = await RefreshTokenCleaner.CleanupAsync(
            context, now, retentionDays: 1, batchSize: 1);

        // Assert
        Assert.Equal(2, deleted);
        context.ChangeTracker.Clear();
        var remainingHashes = await context.RefreshTokens
            .AsNoTracking()
            .Select(token => token.TokenHash)
            .ToListAsync();

        Assert.DoesNotContain(expiredOldFirst.TokenHash, remainingHashes);
        Assert.DoesNotContain(expiredOldSecond.TokenHash, remainingHashes);
        Assert.Contains(recentExpired.TokenHash, remainingHashes);
        Assert.Contains(active.TokenHash, remainingHashes);
        Assert.Contains(revokedButValid.TokenHash, remainingHashes);
    }

    private static RefreshToken AddToken(
        backend.Data.MtsDbContext context,
        long userId,
        string tokenHash,
        DateTime expiresAt,
        DateTime? revokedAt = null)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };
        context.RefreshTokens.Add(token);
        return token;
    }
}
