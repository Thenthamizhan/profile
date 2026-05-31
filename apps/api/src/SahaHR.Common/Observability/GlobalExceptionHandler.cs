using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SahaHR.Common.Tenancy;

namespace SahaHR.Common.Observability;

/// Converts unhandled exceptions into RFC 7807 ProblemDetails. The full exception is logged with the
/// correlation id + tenant/user for diagnosis, but exception detail is NEVER leaked to clients in
/// Production — the response carries a generic message plus the correlation id the caller can quote.
/// Registered as a singleton (AddExceptionHandler), so the scoped ITenantContext is resolved from the
/// request's service provider rather than injected.
public sealed class GlobalExceptionHandler(
    IHostEnvironment env,
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var correlationId = httpContext.TraceIdentifier;
        var tenant = httpContext.RequestServices.GetService<ITenantContext>();

        logger.LogError(exception,
            "Unhandled exception correlationId={CorrelationId} tenant={TenantId} user={UserId} {Method} {Path}",
            correlationId, tenant?.TenantId, tenant?.UserId, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = env.IsDevelopment() ? exception.ToString() : null,
                Extensions = { ["correlationId"] = correlationId },
            },
        });
    }
}
