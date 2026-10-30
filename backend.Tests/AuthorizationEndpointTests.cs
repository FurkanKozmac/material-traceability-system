using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using backend.Common.Enums;
using backend.Common.Security;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Middlewares;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace backend.Tests;

public sealed class AuthorizationEndpointTests
{
    [Theory]
    [InlineData("DELETE", "/api/chemicals/42")]
    [InlineData("DELETE", "/api/addresses/42")]
    [InlineData("DELETE", "/api/units/UNIT-42")]
    [InlineData("POST", "/api/chemicals")]
    [InlineData("POST", "/api/addresses")]
    [InlineData("POST", "/api/units")]
    [InlineData("PUT", "/api/settings/barcode-format")]
    [InlineData("PUT", "/api/settings/notifications")]
    [InlineData("POST", "/api/settings/test-alert-email")]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/42")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/42/role")]
    [InlineData("PUT", "/api/users/42/active")]
    [InlineData("POST", "/api/users/42/reset-password")]
    [InlineData("DELETE", "/api/users/42")]
    public async Task Operator_IsForbidden_FromAdminOnlyEndpoints(string method, string path)
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            request.Content = path == "/api/batches/scan-label"
                ? CreateMultipartImageContent()
                : new StringContent("{}", Encoding.UTF8, "application/json");
        }
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static MultipartFormDataContent CreateMultipartImageContent()
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "label.png");
        return content;
    }

    [Fact]
    public async Task Operator_CanCompleteRelocationTask()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/relocation-tasks/7/complete")
        {
            Content = new StringContent(
                "{\"unitBarcode\":\"BAR-DEMO-UNIT-004\",\"targetAddressBarcode\":\"ADR-DEMO-RACK-SOL-02\"}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanConsumeSeededDemoUnit()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/units/BAR-DEMO-UNIT-001/consume")
        {
            Content = JsonContent.Create(new ConsumeUnitRequest("BAT-DEMO-BAT-001"))
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Consume_WithoutBody_ReturnsBatchRequired()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/units/BAR-DEMO-UNIT-001/consume");
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("BATCH_REQUIRED", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Operator_CanScanLabelAndReceiveParsedSuggestions()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var content = CreateMultipartImageContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches/scan-label") { Content = content };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<BatchLabelScanResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OCR-LOT-42", result?.ExtractedLotNumber);
        Assert.Equal("BAT-OCR-LOT-42", result?.SuggestedBarcodePayload);
    }

    [Fact]
    public async Task OcrDisabled_HostStartsAndHealthEndpointReturnsOk()
    {
        using var host = CreateHost(realOcrSettings: new Dictionary<string, string?> { ["Ocr:Enabled"] = "false" });
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OcrDisabled_ScanLabelReturnsServiceUnavailableWithMeaningfulJson()
    {
        using var host = CreateHost(realOcrSettings: new Dictionary<string, string?> { ["Ocr:Enabled"] = "false" });
        using var client = host.GetTestClient();
        using var content = CreateMultipartImageContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches/scan-label") { Content = content };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("OCR_DISABLED", payload.RootElement.GetProperty("errorCode").GetString());
        Assert.Contains("kapalı", payload.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task OcrUnreachable_ScanLabelFailsFastWithServiceUnavailable()
    {
        // Nothing listens on the discard port, so the connection is refused (or times out after 1 s).
        using var host = CreateHost(realOcrSettings: new Dictionary<string, string?>
        {
            ["Ocr:Enabled"] = "true",
            ["Ocr:Mode"] = "Remote",
            ["Ocr:Endpoint"] = "http://127.0.0.1:9/ocr",
            ["Ocr:ConnectTimeoutSeconds"] = "1",
            ["Ocr:TimeoutSeconds"] = "5"
        }, environmentName: "Testing");
        using var client = host.GetTestClient();
        using var content = CreateMultipartImageContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches/scan-label") { Content = content };
        request.Headers.Add("X-Test-Roles", "Operator");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var response = await client.SendAsync(request);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("OCR_UNAVAILABLE", payload.RootElement.GetProperty("errorCode").GetString());
        Assert.Contains("ulaşılamadı", payload.RootElement.GetProperty("message").GetString());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"OCR failure took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Operator_CanCreateBatch()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches")
        {
            Content = JsonContent.Create(new CreateBatchRequest(
                "LOT-RECEIVED-42", "Fictional Supplier", 4,
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 1))
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanCreateRelocationTask()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/relocation-tasks")
        {
            Content = JsonContent.Create(new CreateRelocationTaskRequest(7, 12))
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanReadDashboardAlerts_AndAnonymousIsRejected()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var anonymous = await client.GetAsync("/api/dashboard/alerts");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/alerts");
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutIdentity_ReturnsUnauthorized()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/api/chemicals");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutRole_IsForbidden_OnStandardGetEndpoint()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/chemicals");
        request.Headers.Add("X-Test-Authenticated", "true");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutRole_IsForbidden_OnUserManagementEndpoint()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("X-Test-Authenticated", "true");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpoints_WithoutToken_ReturnSuccess()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        using var healthResponse = await client.GetAsync("/api/health");
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("demo", "demo123", "DB"));
        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenRequest("refresh-token"));
        using var changePasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("demo", "demo123", "new-demo-password"));
        using var safetyResponse = await client.PostAsJsonAsync(
            "/api/safetyassistant/ask", new SafetyQuestionRequest("CHEM-1", "How should it be stored?"));

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, safetyResponse.StatusCode);
    }

    [Fact]
    public void ChangePasswordEndpoint_UsesLoginRateLimitPolicy()
    {
        var method = typeof(backend.Controllers.AuthController)
            .GetMethod(nameof(backend.Controllers.AuthController.ChangePassword));
        var attribute = method?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("login", attribute.PolicyName);
    }

    [Fact]
    public async Task Admin_CanListUsers()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeleteChemical()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chemicals/42");
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Operator_IsForbidden_FromBulkChemicalImport()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "chemicals.xlsx");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chemicals/bulk-import")
        {
            Content = content
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDownloadChemicalImportTemplate()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/chemicals/excel-template");
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Admin_CanBulkImportChemicals()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "chemicals.xlsx");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chemicals/bulk-import")
        {
            Content = content
        };
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<BulkImportResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRows);
    }

    [Fact]
    public async Task Operator_IsForbidden_FromChemicalImportTemplate()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/chemicals/excel-template");
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized_ForChemicalImportTemplate()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/api/chemicals/excel-template");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized_ForBulkChemicalImport()
    {
        var chemicalExcel = CreateChemicalExcelMock(EmptyImportResult());
        using var host = CreateHost(chemicalExcel);
        using var client = host.GetTestClient();
        using var request = CreateBulkImportRequest(role: "");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        chemicalExcel.Verify(service => service.ImportAsync(
            It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_BulkImportDryRun_IsForwardedToService()
    {
        var chemicalExcel = CreateChemicalExcelMock(EmptyImportResult(dryRun: true));
        using var host = CreateHost(chemicalExcel);
        using var client = host.GetTestClient();
        using var request = CreateBulkImportRequest("Admin", "?dryRun=true");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        chemicalExcel.Verify(service => service.ImportAsync(
            It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_BulkImportWithInvalidRows_ReturnsUnprocessableEntityWithReport()
    {
        var chemicalExcel = CreateChemicalExcelMock(EmptyImportResult(failedCount: 2));
        using var host = CreateHost(chemicalExcel);
        using var client = host.GetTestClient();
        using var request = CreateBulkImportRequest("Admin");

        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<BulkImportResultDto>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Committed);
        Assert.Equal(2, result.FailedCount);
    }

    [Fact]
    public async Task Admin_BulkImportWithNonXlsxExtension_IsRejectedBeforeService()
    {
        var chemicalExcel = CreateChemicalExcelMock(EmptyImportResult());
        using var host = CreateHost(chemicalExcel);
        using var client = host.GetTestClient();
        using var request = CreateBulkImportRequest("Admin", fileName: "chemicals.xlsm");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        chemicalExcel.Verify(service => service.ImportAsync(
            It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_BulkImportAboveFormLimit_IsRejectedBeforeService()
    {
        var chemicalExcel = CreateChemicalExcelMock(EmptyImportResult());
        using var host = CreateHost(chemicalExcel);
        using var client = host.GetTestClient();
        // Default ExcelImport:MaxFileBytes is 2 MB; the filter allows only 64 KB of multipart overhead on top.
        using var request = CreateBulkImportRequest("Admin", body: new byte[3 * 1024 * 1024]);

        using var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,
            $"Unexpected status {response.StatusCode}");
        chemicalExcel.Verify(service => service.ImportAsync(
            It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Operator_CanExportInventoryReport()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/export-excel");
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DemoSeed_CanSignInWithOperatorRole()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ADMIN_PASSWORD"] = "test-admin-password-123",
            ["DEMO_USER_ENABLED"] = "true"
        }).Build();
        await DbInitializer.SeedAsync(database.Context, configuration, isDevelopment: false);

        var login = await AuthServiceTests.CreateService(database.Context)
            .LoginAsync(new LoginRequest("demo", "demo123", "DB"));

        Assert.Equal("Operator", login.Role);
        Assert.Contains("Operator", login.Roles);
        Assert.DoesNotContain("Admin", login.Roles);
    }

    private static BulkImportResultDto EmptyImportResult(bool dryRun = false, int failedCount = 0) =>
        new(dryRun, !dryRun && failedCount == 0, 0, 0, 0, failedCount, [], []);

    private static Mock<IChemicalExcelService> CreateChemicalExcelMock(BulkImportResultDto importResult)
    {
        var chemicalExcel = new Mock<IChemicalExcelService>();
        chemicalExcel.Setup(service => service.CreateTemplateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        chemicalExcel.Setup(service => service.ImportAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);
        return chemicalExcel;
    }

    private static HttpRequestMessage CreateBulkImportRequest(
        string role,
        string query = "",
        byte[]? body = null,
        string fileName = "chemicals.xlsx")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(body ?? [1, 2, 3]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", fileName);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chemicals/bulk-import" + query)
        {
            Content = content
        };
        if (role.Length > 0)
            request.Headers.Add("X-Test-Roles", role);
        return request;
    }

    private static IConfiguration TestAuthenticationConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-thirty-two-bytes-long",
            ["Jwt:Issuer"] = "backend-tests",
            ["Jwt:Audience"] = "backend-tests"
        }).Build();

    private static IHost CreateHost(
        Mock<IChemicalExcelService>? chemicalExcelOverride = null,
        IReadOnlyDictionary<string, string?>? realOcrSettings = null,
        string? environmentName = null)
    {
        var chemical = new Mock<IChemicalService>();
        chemical.Setup(service => service.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var chemicalExcel = chemicalExcelOverride ?? CreateChemicalExcelMock(EmptyImportResult());
        var inventoryReport = new Mock<IInventoryReportService>();
        inventoryReport.Setup(service => service.ExportAuditReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        var addresses = new Mock<IAddressService>();
        var units = new Mock<IUnitService>();
        units.Setup(service => service.ConsumeAsync(
                "BAR-DEMO-UNIT-001", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnitResponse(1, "BAR-DEMO-UNIT-001", "Depleted", null, null, 1,
                "DEMO-BAT-001", "Polymer Stabilizer M-01", "DEMO-CHM-01", DateTime.UtcNow.AddDays(180), false));
        var settings = new Mock<ISettingsService>();
        var email = new Mock<IEmailService>();
        var dashboardAlerts = new Mock<IDashboardAlertService>();
        dashboardAlerts.Setup(service => service.GetAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardAlertsResponse(30, 0, [], 0, []));
        var relocationTasks = new Mock<IRelocationTaskService>();
        relocationTasks.Setup(service => service.CreateTaskAsync(
                It.IsAny<CreateRelocationTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelocationTaskResponse(7, "Pending", DateTime.UtcNow, null,
                1, "BAR-DEMO-UNIT-001", 3, "ADR-DEMO-RACK-01", 12,
                "ADR-DEMO-RACK-SOL-02", null, null));
        relocationTasks.Setup(service => service.CompleteTaskAsync(
                It.IsAny<long>(), It.IsAny<CompleteRelocationTaskRequest>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var ocr = new Mock<IOcrService>();
        ocr.Setup(service => service.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SODIUM HYDROXIDE LOT: OCR-LOT-42 EXP: 2027-11-15");
        var labelParser = new Mock<ILabelParserService>();
        labelParser.Setup(service => service.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchLabelScanResultDto(1, "Sodium Hydroxide", "OCR-LOT-42",
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc),
                "SODIUM HYDROXIDE LOT: OCR-LOT-42 EXP: 2027-11-15", "BAT-OCR-LOT-42"));
        var batches = new Mock<IBatchService>();
        batches.Setup(service => service.CreateAsync(It.IsAny<CreateBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchResponse(42, "LOT-RECEIVED-42", "Fictional Supplier", 4,
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 1, "Sodium Hydroxide", 4));
        var loginResponse = new LoginResponse(
            "access-token", "access-token", "refresh-token", "demo", "Operator",
            ["Operator"], new UserDto(1, "demo", "Operator", ["Operator"]));
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);
        auth.Setup(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);
        auth.Setup(service => service.ChangePasswordAsync(
                It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var users = new Mock<IUserService>();
        users.Setup(service => service.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var rag = new Mock<IMsdsRagService>();
        rag.Setup(service => service.AskQuestionAsync(
                It.IsAny<SafetyQuestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafetyAnswerResponse(
                "CHEM-1", "How should it be stored?", "Store it safely.", "Storage", true));

        var hostBuilder = new HostBuilder();
        if (environmentName is not null)
            hostBuilder.UseEnvironment(environmentName);

        return hostBuilder
            .ConfigureAppConfiguration(configuration =>
            {
                if (realOcrSettings is not null)
                    configuration.AddInMemoryCollection(realOcrSettings);
            })
            .ConfigureWebHost(webHost => webHost
            .UseTestServer()
            .ConfigureServices((context, services) =>
            {
                services.AddRouting();
                services.AddMtsAuthentication(TestAuthenticationConfiguration());
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.FallbackPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .RequireRole("Admin", "Operator")
                        .Build();
                });
                // BatchesController broadcasts inventory changes after creation.
                // The production host registers SignalR in Program.cs; this isolated
                // test host must register it as well so controller activation matches.
                services.AddSignalR();
                services.AddControllers().AddApplicationPart(typeof(backend.Controllers.ChemicalController).Assembly);
                services.AddDbContext<MtsDbContext>(options => options.UseSqlite("Data Source=:memory:"));
                services.AddSingleton(chemical.Object);
                services.AddSingleton(chemicalExcel.Object);
                services.AddSingleton(inventoryReport.Object);
                services.AddSingleton(addresses.Object);
                services.AddSingleton(units.Object);
                services.AddSingleton(settings.Object);
                services.AddSingleton(email.Object);
                services.AddSingleton(relocationTasks.Object);
                services.AddSingleton(dashboardAlerts.Object);
                services.AddSingleton(batches.Object);
                // With realOcrSettings the production registration and OcrService are used instead of the mock.
                if (realOcrSettings is not null)
                    services.AddMtsOcr(context.Configuration);
                else
                    services.AddSingleton(ocr.Object);
                services.AddSingleton(labelParser.Object);
                services.AddSingleton(auth.Object);
                services.AddSingleton(users.Object);
                services.AddSingleton(rag.Object);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }))
            .Start();
    }

    private sealed class TestRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var rolesHeader = Request.Headers["X-Test-Roles"].ToString();
            var authenticatedWithoutRoles = string.Equals(
                Request.Headers["X-Test-Authenticated"].ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rolesHeader) && !authenticatedWithoutRoles)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "42"),
                new(ClaimTypes.Name, "authorization-test")
            };
            claims.AddRange(rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
