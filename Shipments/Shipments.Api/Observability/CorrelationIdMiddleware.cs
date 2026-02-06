using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Shipments.Api.Observability;

public sealed class CorrelationIdMiddleware : IMiddleware
{
    private readonly CorrelationOptions _options;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        IOptions<CorrelationOptions> options,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var header = _options.HeaderName;

        var correlationId =
            context.Request.Headers.TryGetValue(header, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;

        context.Response.Headers[header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
