using backend.Common.Exceptions;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;

namespace backend.Tests;

public sealed class OcrServiceTests
{
    [Fact]
    public async Task SampleMode_ReturnsConfiguredFixtureTextOutsideProduction()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Sample",
            ["Ocr:SampleText"] = "LOT: LOCAL-123 EXP: 2027-11-15"
        }).Build();
        var service = new OcrService(new HttpClient(), configuration, CreateEnvironment("Testing"), NullLogger<OcrService>.Instance);

        var text = await service.ExtractTextAsync(new MemoryStream([1, 2, 3]), "image/png");

        Assert.Equal("LOT: LOCAL-123 EXP: 2027-11-15", text);
    }

    [Fact]
    public async Task RemoteModeWithoutEndpoint_ReportsMeaningfulUnavailableError()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Remote"
        }).Build();
        var service = new OcrService(new HttpClient(), configuration, CreateEnvironment("Production"), NullLogger<OcrService>.Instance);

        var error = await Assert.ThrowsAsync<OcrServiceUnavailableException>(() =>
            service.ExtractTextAsync(new MemoryStream([1]), "image/jpeg"));

        Assert.Contains("Ocr:Endpoint", error.Message);
    }

    [Fact]
    public async Task ProductionRemoteMode_RejectsExternalOcrEndpoint()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Remote",
            ["Ocr:Endpoint"] = "http://ocr.example.test/parse"
        }).Build();
        var service = new OcrService(new HttpClient(), configuration, CreateEnvironment("Production"), NullLogger<OcrService>.Instance);

        var error = await Assert.ThrowsAsync<OcrServiceUnavailableException>(() =>
            service.ExtractTextAsync(new MemoryStream([1]), "image/jpeg"));

        Assert.Contains("ocr-service", error.Message);
    }

    [Fact]
    public async Task ProductionRemoteMode_AllowsInternalPaddleOcrEndpoint()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"text\":\"LOT: LOCAL-42\"}", Encoding.UTF8, "application/json")
        }));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Remote",
            ["Ocr:Endpoint"] = "http://ocr-service:8080/ocr"
        }).Build();
        var service = new OcrService(client, configuration, CreateEnvironment("Production"), NullLogger<OcrService>.Instance);

        var text = await service.ExtractTextAsync(new MemoryStream([1, 2, 3]), "image/png");

        Assert.Equal("LOT: LOCAL-42", text);
    }

    [Fact]
    public async Task ProductionCannotUseSampleMode()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Sample"
        }).Build();
        var service = new OcrService(new HttpClient(), configuration, CreateEnvironment("Production"), NullLogger<OcrService>.Instance);

        await Assert.ThrowsAsync<OcrServiceUnavailableException>(() =>
            service.ExtractTextAsync(new MemoryStream([1]), "image/jpeg"));
    }

    [Fact]
    public async Task RemoteModePostsImageAndParsesPaddleOcrContract()
    {
        string? requestMediaType = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestMediaType = request.Content?.Headers.ContentType?.MediaType;
            var multipartBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("name=file", multipartBody);
            Assert.DoesNotContain("name=options", multipartBody);
            Assert.EndsWith("/ocr", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"LOT: REMOTE-42\"}", Encoding.UTF8, "application/json")
            };
        }));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ocr:Mode"] = "Remote",
            ["Ocr:Endpoint"] = "https://ocr.example.test/ocr"
        }).Build();
        var service = new OcrService(client, configuration, CreateEnvironment("Testing"), NullLogger<OcrService>.Instance);

        var text = await service.ExtractTextAsync(new MemoryStream([1, 2, 3]), "image/png");

        Assert.Equal("LOT: REMOTE-42", text);
        Assert.Equal("multipart/form-data", requestMediaType);
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
