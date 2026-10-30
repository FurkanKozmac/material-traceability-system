using System.Net;
using System.Text.Json;
using backend.Common.Exceptions;

namespace backend.Middlewares;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction() &&
                ex is not NotFoundException and not BusinessRuleException and not OcrServiceUnavailableException)
            {
                throw;
            }

            logger.LogError(ex, "Hata yakalandı: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = ex switch
        {
            NotFoundException => (HttpStatusCode.NotFound, ex.Message),
            BusinessRuleException => (HttpStatusCode.BadRequest, ex.Message),
            // Unexpected exceptions (including InvalidOperationException) fall through to a generic 500.
            OcrServiceUnavailableException => (HttpStatusCode.ServiceUnavailable, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "Sunucu tarafında beklenmeyen bir hata oluştu.")
        };

        context.Response.StatusCode = (int)statusCode;

        var result = new
        {
            statusCode = (int)statusCode,
            message,
            errorCode = ex switch
            {
                BusinessRuleException businessRule => businessRule.ErrorCode,
                OcrServiceUnavailableException ocr => ocr.ErrorCode,
                _ => null
            },
            details = ex is BusinessRuleException rule ? rule.Details : null,
            timestamp = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
}
