using backend.Common.Events;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using backend.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests;

public class LowStockAlertBackgroundServiceTests
{
    [Fact]
    public async Task UnconfiguredSmtp_SkipsEmailsAndLogsOnce()
    {
        // Arrange
        var email = new Mock<IEmailService>();
        var services = new ServiceCollection();
        services.AddSingleton(email.Object);
        await using var provider = services.BuildServiceProvider();
        var logger = new CapturingLogger();
        var service = new LowStockAlertBackgroundService(
            new FixedLowStockChannel(CreateAlert(1), CreateAlert(2), CreateAlert(3)),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SmtpOptions { Host = "" }),
            logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        email.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("SMTP yapılandırılmamış", entry.Message);
    }

    [Fact]
    public async Task MailKitEmailService_WithBlankHost_ThrowsSoTestEmailEndpointReturns503()
    {
        var service = new MailKitEmailService(
            Options.Create(new SmtpOptions { Host = " " }), Mock.Of<IHostEnvironment>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendEmailAsync("ops@example.com", "subject", "<p>body</p>"));
    }

    [Fact]
    public void BlankEnvironmentValue_OverridesAppsettingsHost()
    {
        // Compose passes Smtp__Host="" when SMTP_HOST is blank; it must win over appsettings' localhost.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "localhost" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "" })
            .Build();

        var options = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>()!;

        Assert.False(options.IsConfigured);
    }

    private static LowStockAlertEvent CreateAlert(int chemicalId) =>
        new(chemicalId, "Demo Chemical", "DEMO-CHM", "DEMO-BAT", 1, 5, DateTime.UtcNow);

    private sealed class FixedLowStockChannel(params LowStockAlertEvent[] alerts) : ILowStockChannel
    {
        public ValueTask PublishAlertAsync(LowStockAlertEvent alert, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<LowStockAlertEvent> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var alert in alerts)
            {
                await Task.Yield();
                yield return alert;
            }
        }
    }

    private sealed class CapturingLogger : ILogger<LowStockAlertBackgroundService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
