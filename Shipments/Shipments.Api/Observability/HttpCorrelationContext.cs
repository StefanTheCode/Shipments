using Shipments.Application.Abstractions;

namespace Shipments.Api.Observability;

public sealed class HttpCorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _accessor;
    private readonly CorrelationOptions _options;

    public HttpCorrelationContext(
        IHttpContextAccessor accessor,
        Microsoft.Extensions.Options.IOptions<CorrelationOptions> options)
    {
        _accessor = accessor;
        _options = options.Value;
    }

    public string CorrelationId
    {
        get
        {
            var http = _accessor.HttpContext;
            if (http is null)
            {
                return Guid.NewGuid().ToString("N");
            }

            if (http.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            if (http.Request.Headers.TryGetValue(_options.HeaderName, out var hv) && !string.IsNullOrWhiteSpace(hv))
            {
                return hv.ToString();
            }

            return Guid.NewGuid().ToString("N");
        }
    }
}