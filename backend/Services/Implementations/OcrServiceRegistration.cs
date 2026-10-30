using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public static class OcrServiceRegistration
{
    public const int DefaultTimeoutSeconds = 20;
    public const int DefaultConnectTimeoutSeconds = 3;

    /// <summary>
    /// Registers the OCR client without contacting the OCR service, so the backend starts even when
    /// the optional ocr-service container is not running. The short connect timeout makes an
    /// unreachable service fail fast with 503 instead of holding the request open.
    /// </summary>
    public static IServiceCollection AddMtsOcr(this IServiceCollection services, IConfiguration configuration)
    {
        var timeout = ReadSeconds(configuration, "Ocr:TimeoutSeconds", DefaultTimeoutSeconds);
        var connectTimeout = ReadSeconds(configuration, "Ocr:ConnectTimeoutSeconds", DefaultConnectTimeoutSeconds);

        services.AddHttpClient<IOcrService, OcrService>(client => client.Timeout = timeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = connectTimeout });
        return services;
    }

    private static TimeSpan ReadSeconds(IConfiguration configuration, string key, int defaultSeconds)
    {
        var seconds = configuration.GetValue(key, defaultSeconds);
        return TimeSpan.FromSeconds(seconds > 0 ? seconds : defaultSeconds);
    }
}
