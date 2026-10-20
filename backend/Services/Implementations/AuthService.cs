using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using backend.Common.Exceptions;
using backend.Common.Security;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services.Implementations;

public class AuthService(
    MtsDbContext context,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    IMemoryCache memoryCache,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var failureKey = GetLoginFailureKey(request.Username);
        var accountLockoutEnabled = !string.Equals(request.Username, "demo", StringComparison.OrdinalIgnoreCase);
        if (accountLockoutEnabled)
            EnsureLoginAllowed(failureKey);

        BusinessRuleException LoginFailureForRequest(string message) =>
            accountLockoutEnabled ? LoginFailure(failureKey, message) : new BusinessRuleException(message);

        if (!string.Equals(request.AuthSource ?? "DB", "DB", StringComparison.OrdinalIgnoreCase))
            throw LoginFailureForRequest("LDAP kimlik doğrulaması henüz yapılandırılmamıştır.");

        var user = await GetUserAsync(request.Username, ct);
        if (user is null)
            throw LoginFailureForRequest("Geçersiz kullanıcı adı veya şifre!");

        var verification = VerifyPassword(user, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            throw LoginFailureForRequest("Geçersiz kullanıcı adı veya şifre!");

        if (!user.Active)
            throw LoginFailureForRequest("Geçersiz kullanıcı adı veya şifre!");

        memoryCache.Remove(failureKey);

        var roles = GetRequiredRoles(user);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.Password = passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(ct);
        }

        var issued = CreateTokens(user, roles);
        context.RefreshTokens.Add(CreateRefreshToken(user.Id, issued.RefreshTokenHash));
        await context.SaveChangesAsync(ct);
        return issued.Response;
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var principal = ValidateToken(refreshToken, "refresh");
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
            throw new BusinessRuleException("Geçersiz yenileme anahtarı.");

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var user = await context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Active, ct);
        if (user is null)
            throw new BusinessRuleException("Oturum yenilenemedi.");

        var roles = GetRequiredRoles(user);

        var tokenHash = HashToken(refreshToken);
        var storedToken = await context.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE \"TokenHash\" = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(t => t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow, ct);
        if (storedToken is null || storedToken.UserId != userId)
            throw new BusinessRuleException("Oturum yenilenemedi.");

        var issued = CreateTokens(user, roles);
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = issued.RefreshTokenHash;
        context.RefreshTokens.Add(CreateRefreshToken(user.Id, issued.RefreshTokenHash));
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return issued.Response;
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var failureKey = GetLoginFailureKey(request.Username);
        var accountLockoutEnabled = !string.Equals(request.Username, "demo", StringComparison.OrdinalIgnoreCase);
        if (accountLockoutEnabled)
            EnsureLoginAllowed(failureKey);

        BusinessRuleException ChangePasswordFailure(string message) =>
            accountLockoutEnabled ? LoginFailure(failureKey, message) : new BusinessRuleException(message);

        var user = await GetUserAsync(request.Username, ct);
        if (user is null)
            throw ChangePasswordFailure("Geçersiz kullanıcı adı veya parola!");

        if (VerifyPassword(user, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw ChangePasswordFailure("Geçersiz kullanıcı adı veya parola!");

        if (!user.Active ||
            !string.Equals(user.AuthSource, "DB", StringComparison.OrdinalIgnoreCase) ||
            user.Roles.Count == 0)
        {
            throw ChangePasswordFailure("Geçersiz kullanıcı adı veya parola!");
        }

        PasswordPolicy.Validate(request.NewPassword, request.CurrentPassword);
        memoryCache.Remove(failureKey);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        user.Password = passwordHasher.HashPassword(user, request.NewPassword);
        await RefreshTokenRevocation.RevokeAllActiveAsync(context, user.Id, ct);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var token = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == HashToken(refreshToken) && t.RevokedAt == null, ct);
        if (token is null) return;
        token.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    private Task<User?> GetUserAsync(string username, CancellationToken ct) =>
        context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    private PasswordVerificationResult VerifyPassword(User user, string password)
    {
        if (user.Password.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, user.Password)
                    ? PasswordVerificationResult.SuccessRehashNeeded
                    : PasswordVerificationResult.Failed;
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return PasswordVerificationResult.Failed;
            }
        }
        if (!IsHashed(user.Password))
            return PasswordVerificationResult.Failed;
        return passwordHasher.VerifyHashedPassword(user, user.Password, password);
    }

    private static bool IsHashed(string password) => password.StartsWith("AQAAAA", StringComparison.Ordinal);

    private static List<string> GetRequiredRoles(User user)
    {
        var roles = user.Roles.Select(r => r.Name).Distinct().ToList();
        if (roles.Count == 0)
            throw new BusinessRuleException("Kullanıcıya atanmış bir rol bulunamadı.", "USER_HAS_NO_ROLE");
        return roles;
    }

    private (LoginResponse Response, string RefreshTokenHash) CreateTokens(User user, List<string> roles)
    {
        var accessToken = CreateToken(user, roles, "access", TimeSpan.FromMinutes(GetInt("AccessTokenMinutes", 30)));
        var refreshToken = CreateToken(user, roles, "refresh", TimeSpan.FromDays(GetInt("RefreshTokenDays", 7)));
        var dto = new UserDto(user.Id, user.Username, roles[0], roles);
        return (new LoginResponse(accessToken, accessToken, refreshToken, user.Username, roles[0], roles, dto), HashToken(refreshToken));
    }

    private RefreshToken CreateRefreshToken(long userId, string tokenHash) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = DateTime.UtcNow.AddDays(GetInt("RefreshTokenDays", 7))
    };

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string GetLoginFailureKey(string username)
    {
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"login-failure:{ip}:{username.Trim().ToUpperInvariant()}";
    }

    private void EnsureLoginAllowed(string key)
    {
        if (memoryCache.TryGetValue<int>(key, out var failures) && failures >= 5)
            throw new BusinessRuleException("Çok fazla başarısız giriş denemesi. Lütfen bir dakika sonra tekrar deneyin.");
    }

    private BusinessRuleException LoginFailure(string key, string message)
    {
        var failures = memoryCache.Get<int?>(key) ?? 0;
        memoryCache.Set(key, failures + 1, TimeSpan.FromMinutes(1));
        return new BusinessRuleException(message);
    }

    private string CreateToken(User user, IEnumerable<string> roles, string tokenType, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("token_type", tokenType)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token, string expectedType)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, TokenValidationParameters(), out _);
            if (principal.FindFirstValue("token_type") != expectedType)
                throw new SecurityTokenException("Yanlış token türü.");
            return principal;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            throw new BusinessRuleException("Geçersiz veya süresi dolmuş yenileme anahtarı.");
        }
    }

    private TokenValidationParameters TokenValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = GetSigningKey(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private SymmetricSecurityKey GetSigningKey() =>
        new(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key yapılandırılmalıdır.")));

    private int GetInt(string key, int fallback) =>
        int.TryParse(configuration[$"Jwt:{key}"], out var value) ? value : fallback;
}
