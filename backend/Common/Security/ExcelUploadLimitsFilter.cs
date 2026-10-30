using backend.Services.Implementations.Excel;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace backend.Common.Security;

/// <summary>
/// Applies the configured Excel upload limits before model binding reads the form.
/// The memory buffer threshold is above the body limit, so the limit is hit before the upload could spill to a temp file.
/// </summary>
public sealed class ExcelUploadLimitsFilter(IOptions<ExcelImportOptions> options) : IResourceFilter
{
    // Multipart boundaries and headers add a little on top of the file itself.
    private const long MultipartOverheadBytes = 64 * 1024;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var limit = options.Value.MaxFileBytes + MultipartOverheadBytes;
        var features = context.HttpContext.Features;

        var bodySizeFeature = features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
            bodySizeFeature.MaxRequestBodySize = limit;

        if (features.Get<IFormFeature>()?.Form is null)
        {
            features.Set<IFormFeature>(new FormFeature(context.HttpContext.Request, new FormOptions
            {
                MultipartBodyLengthLimit = limit,
                MemoryBufferThreshold = (int)Math.Min(limit + 1, int.MaxValue),
                BufferBodyLengthLimit = limit,
                ValueCountLimit = 16
            }));
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
