using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MiniBank.Shared.Domain.Exceptions;

namespace MiniBank.Shared.Infrastructure.Api;

public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            ConcurrencyConflictException => (HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT"),
            DomainException domainEx => (HttpStatusCode.UnprocessableEntity, domainEx.Code),
            ArgumentException => (HttpStatusCode.BadRequest, "INVALID_ARGUMENT"),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
        };

        var problemDetails = new
        {
            type = $"https://minibank.dev/errors/{errorCode.ToLowerInvariant()}",
            title = exception.Message,
            status = (int)statusCode,
            errorCode,
            traceId = context.TraceIdentifier
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
