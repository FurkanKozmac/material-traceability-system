using System.Net;
using System.Threading.RateLimiting;
using backend.Common.Security;
using backend.DTOs;
using backend.Services.Interfaces;
using Moq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace backend.Tests;

public sealed class SecurityHardeningTests
{
    [Fact]
    public void LoginRateLimit_DefaultIsTenPerMinute()
    {
        Assert.Equal(10, LoginRateLimitPartition.DefaultPermitLimit);
        Assert.Equal(60, LoginRateLimitPartition.DefaultWindowSeconds);
    }

    [Fact]
    public async Task LoginRateLimit_UsesForwardedClientIpForSeparateBuckets()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                        options.ForwardLimit = 1;
                        options.KnownProxies.Clear();
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                    });
                    services.AddRouting();
                    services.AddRateLimiter(options =>
                    {
                        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                        options.AddPolicy("login-test", context => LoginRateLimitPartition.Create(context, 1, 60));
                    });
                })
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints => endpoints
                        .MapPost("/api/auth/login", () => Results.Ok())
                        .RequireRateLimiting("login-test"));
                }))
            .Start();

        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await SendLoginAsync(client, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendLoginAsync(client, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendLoginAsync(client, "203.0.113.11")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendLoginAsync(client, "203.0.113.11")).StatusCode);
    }

    [Fact]
    public async Task SafetyAssistant_IsAnonymousAndRateLimitedPerForwardedIp()
    {
        var ragService = new Mock<IMsdsRagService>();
        ragService.Setup(service => service.AskQuestionAsync(It.IsAny<SafetyQuestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafetyAnswerResponse("CHEM-1", "question", "answer", "section", true));

        using var host = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                        options.ForwardLimit = 1;
                        options.KnownProxies.Clear();
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                    });
                    services.AddRouting();
                    services.AddRateLimiter(options =>
                    {
                        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                        options.AddPolicy("safety-assistant", context => LoginRateLimitPartition.Create(context, 1, 60));
                    });
                    services.AddControllers().AddApplicationPart(typeof(backend.Controllers.SafetyAssistantController).Assembly);
                    services.AddSingleton(ragService.Object);
                })
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Start();

        using var client = host.GetTestClient();
        const string body = "{\"chemicalCode\":\"CHEM-1\",\"question\":\"How should it be stored?\"}";

        Assert.Equal(HttpStatusCode.OK, (await SendSafetyQuestionAsync(client, body, "203.0.113.20")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendSafetyQuestionAsync(client, body, "203.0.113.20")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendSafetyQuestionAsync(client, body, "203.0.113.21")).StatusCode);
    }

    [Fact]
    public void ProductionValidator_RejectsDefaultAdminPassword()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                "valid-jwt-secret-that-is-at-least-32-characters-long",
                "admin123",
                "Host=db;Database=mts;Username=mts;Password=long-random-password-value"));

        Assert.Contains("ADMIN_PASSWORD", exception.Message);
    }

    [Fact]
    public void ProductionValidator_RejectsDefaultPostgresPassword()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                "valid-jwt-secret-that-is-at-least-32-characters-long",
                "unique-admin-password-98765",
                "Host=db;Database=mts;Username=mts;Password=postgres"));

        Assert.Contains("non-default password", exception.Message);
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendSafetyQuestionAsync(HttpClient client, string body, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/safetyassistant/ask")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }
}
