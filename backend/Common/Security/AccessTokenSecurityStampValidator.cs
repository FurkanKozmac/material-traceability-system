using System.Security.Claims;
using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Common.Security;

public sealed class AccessTokenSecurityStampValidator(MtsDbContext context)
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
            return false;

        var tokenStamp = principal?.FindFirstValue("sstamp");
        if (string.IsNullOrWhiteSpace(tokenStamp))
            return false;

        var user = await context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Active, item.SecurityStamp })
            .SingleOrDefaultAsync(ct);

        return user is { Active: true } &&
               !string.IsNullOrWhiteSpace(user.SecurityStamp) &&
               string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal);
    }
}
