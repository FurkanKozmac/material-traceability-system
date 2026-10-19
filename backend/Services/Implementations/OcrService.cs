using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Common.Exceptions;
using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public sealed class OcrService(
    HttpClient httpClient,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<OcrService> logger) : IOcrService
{
    private const string DefaultSampleText = "SUPPLIER: SAMPLE MATERIALS - LOT: DEMO-LOT-001 - EXP: 15.11.2027";
    private const int MaxRawTextLength = 20_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> ExtractTextAsync(Stream image, string contentType, CancellationToken ct = default)
    {
        var mode = configuration["Ocr:Mode"];
        if (string.IsNullOrWhiteSpace(mode))
            mode = environment.IsDevelopment() ? "Sample" : "Remote";

        if (string.Equals(mode, "Sample", StringComparison.OrdinalIgnoreCase))
        {
            if (environment.IsProduction())
                throw new OcrServiceUnavailableException("Örnek OCR modu Production ortamında kullanılamaz.");

            return ValidateText(configuration["Ocr:SampleText"] ?? DefaultSampleText);
        }

        if (!string.Equals(mode, "Remote", StringComparison.OrdinalIgnoreCase))
            throw new OcrServiceUnavailableException("OCR modu geçersiz yapılandırılmış.");

        var endpoint = configuration["Ocr:Endpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme is not ("http" or "https"))
            throw new OcrServiceUnavailableException("OCR sağlayıcısı yapılandırılmamış. Ocr:Endpoint ayarlanmalıdır.");
        if (environment.IsProduction() &&
            !endpointUri.Host.Equals("ocr-service", StringComparison.OrdinalIgnoreCase))
            throw new OcrServiceUnavailableException("Production OCR yalnızca iç ağdaki ocr-service üzerinden kullanılabilir.");
        if (environment.IsProduction() && endpointUri.Scheme != Uri.UriSchemeHttp)
            throw new OcrServiceUnavailableException("İç ağdaki ocr-service HTTP kullanmalıdır.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri);
        using var multipart = new MultipartFormDataContent();
        var imageContent = new StreamContent(image);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(imageContent, "file", "label-image");
        request.Content = multipart;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("PaddleOCR service returned HTTP {StatusCode}.", (int)response.StatusCode);
                throw new OcrServiceUnavailableException($"OCR sağlayıcısı HTTP {(int)response.StatusCode} yanıtı verdi.");
            }

            var result = await response.Content.ReadFromJsonAsync<PaddleOcrResponse>(JsonOptions, ct);
            var rawText = result?.Text;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                logger.LogWarning("PaddleOCR service returned no recognized text.");
                throw new OcrServiceUnavailableException("OCR sağlayıcısı okunabilir metin döndürmedi.");
            }

            return ValidateText(rawText);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OcrServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "Could not reach or parse the response from the PaddleOCR service at {Endpoint}.", endpointUri);
            throw new OcrServiceUnavailableException("OCR sağlayıcısına ulaşılamadı veya yanıtı okunamadı.", ex);
        }
    }

    private static string ValidateText(string rawText)
    {
        if (rawText.Length > MaxRawTextLength)
            throw new OcrServiceUnavailableException("OCR sağlayıcısı izin verilen metin uzunluğunu aştı.");
        return rawText;
    }

    private sealed record PaddleOcrResponse(string? Text);
}
