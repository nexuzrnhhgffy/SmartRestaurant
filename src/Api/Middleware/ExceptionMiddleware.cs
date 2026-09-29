using SmartRestaurant.Application.Common;
using System.Text.Json;

namespace SmartRestaurant.Api.Middleware;

/// <summary>Uniform error envelope: { "error": "...", "status": n } for AppException and 500s.</summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log) => (_next, _log) = (next, log);

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (AppException ex)
        {
            ctx.Response.StatusCode = ex.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message, status = ex.StatusCode }));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Internal server error: " + ex.Message, status = 500 }));
        }
    }
}
