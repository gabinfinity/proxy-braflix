namespace M3U8Proxy.Middleware;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        LogRequestInfo(context);

        await _next(context);
    }

    private void LogRequestInfo(HttpContext context)
    {
        context.Request.Headers.TryGetValue("Origin", out var origin);
        context.Request.Headers.TryGetValue("Referer", out var referer);
        var url = context.Request.Path + context.Request.QueryString;
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "no ip";

        _logger.LogInformation($"IPv4 Address: {ipAddress}");

        if (origin.Count > 0)
            _logger.LogInformation($"Origin: {origin}");

        if (referer.Count > 0)
            _logger.LogInformation($"Referer: {referer}");

        _logger.LogInformation($"URL: {url}");
    }
}