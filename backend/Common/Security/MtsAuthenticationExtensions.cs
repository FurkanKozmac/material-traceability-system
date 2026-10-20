using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace backend.Common.Security;

public static class MtsAuthenticationExtensions
{
    public static IServiceCollection AddMtsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key yapılandırılmalıdır.");

        services.AddScoped<AccessTokenSecurityStampValidator>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var requestPath = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            requestPath.StartsWithSegments("/hubs/traceability"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.FindFirst("token_type")?.Value != "access")
                        {
                            context.Fail("Bu uç için access token gereklidir.");
                            return;
                        }

                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<AccessTokenSecurityStampValidator>();
                        if (!await validator.IsValidAsync(
                                context.Principal, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Oturum geçersiz.");
                        }
                    }
                };
            });

        return services;
    }
}
