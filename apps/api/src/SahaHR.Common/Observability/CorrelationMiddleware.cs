using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace SahaHR.Common.Observability;

/// Assigns/propagates a correlation id for every request: honours an inbound X-Correlation-Id (else
/// falls back to the ASP.NET trace id), echoes it on the response, and opens a logging scope so every
/// log line for the request carries {CorrelationId}. With JSON console logging this yields
/// request-correlated, structured, queryable logs.
public sealed class CorrelationMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var v) && !StringValues.IsNullOrEmpty(v)
            ? v.ToString()
            : context.TraceIdentifier;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await _next(context);
    }
}
