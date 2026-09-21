using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services.Implementations;

public class AuthService(
    MtsDbContext context,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (!string.Equals(request.AuthSource ?? "DB", "DB", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("LDAP kimlik doğrulaması henüz yapılandırılmamıştır.");

        var user = await GetUserAsync(request.Username, ct);
        if (user is null || !user.Active)
            throw new BusinessRuleException("Geçersiz kullanıcı adı veya şifre!");

        var verification = VerifyPassword(user, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            throw new BusinessRuleException("Geçersiz kullanıcı adı veya şifre!");

        if (verification == PasswordVerificationResult.SuccessRehashNeeded || !IsHashed(user.Password))
        {
            user.Password = passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(ct);
        }

        return CreateResponse(user);
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var principal = ValidateToken(refreshToken, "refresh");
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
            throw new BusinessRuleException("Geçersiz yenileme anahtarı.");

        var user = await context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Active, ct);
        if (user is null)
            throw new BusinessRuleException("Oturum yenilenemedi.");

        return CreateResponse(user);
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
            return user.Password == password ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Failed;
        return passwordHasher.VerifyHashedPassword(user, user.Password, password);
    }

    private static bool IsHashed(string password) => password.StartsWith("AQAAAA", StringComparison.Ordinal);

    private LoginResponse CreateResponse(User user)
    {
        var roles = user.Roles.Select(r => r.Name).Distinct().ToList();
        if (roles.Count == 0) roles.Add("Operator");
        var accessToken = CreateToken(user, roles, "access", TimeSpan.FromMinutes(GetInt("AccessTokenMinutes", 30)));
        var refreshToken = CreateToken(user, roles, "refresh", TimeSpan.FromDays(GetInt("RefreshTokenDays", 7)));
        var dto = new UserDto(user.Id, user.Username, roles[0], roles);
        return new LoginResponse(accessToken, accessToken, refreshToken, user.Username, roles[0], roles, dto);
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
