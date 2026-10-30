using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Common.Exceptions;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace backend.Tests;

public class ExceptionMappingEndpointTests
{
    private const string InternalDetail = "internal-secret-detail";

    [Fact]
    public async Task UnexpectedInvalidOperationException_Returns500WithoutInternalMessage()
    {
        var (status, body) = await PostRelocationTaskAsync(new InvalidOperationException(InternalDetail));

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.DoesNotContain(InternalDetail, body);
        Assert.Equal("Sunucu tarafında beklenmeyen bir hata oluştu.",
            JsonSerializer.Deserialize<JsonElement>(body).GetProperty("message").GetString());
    }

    [Fact]
    public async Task BusinessRuleException_KeepsMessageAnd400()
    {
        var (status, body) = await PostRelocationTaskAsync(new BusinessRuleException("Hedef adres bulunamadı!"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("Hedef adres bulunamadı!",
            JsonSerializer.Deserialize<JsonElement>(body).GetProperty("message").GetString());
    }

    private static async Task<(HttpStatusCode Status, string Body)> PostRelocationTaskAsync(Exception thrown)
    {
        var service = new Mock<IRelocationTaskService>();
        service.Setup(s => s.CreateTaskAsync(It.IsAny<CreateRelocationTaskRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);
        using var host = RelocationTaskEndpointPostgresTests.CreateHost(services => services.AddSingleton(service.Object));
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/relocation-tasks")
        {
            Content = JsonContent.Create(new CreateRelocationTaskRequest(1, 2))
        };
        request.Headers.Add(HeaderRoleAuthenticationHandler.RolesHeader, "Operator");

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
