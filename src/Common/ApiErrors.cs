using Bank.Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Http;

public sealed class ApiErrors(ILogger<ApiErrors> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            BusinessException b => (b.Status, b.Message),
            DbUpdateConcurrencyException => (409, "La información cambió por otra operación. Consulte el estado y reintente."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
                (409, "Ya existe un registro con esa identificación, número de cuenta o clave de operación."),
            BadHttpRequestException => (400, "Solicitud inválida. Revise los campos y sus tipos."),
            _ => (500, "Ocurrió un error interno.")
        };
        if (status >= 500) logger.LogError(exception, "Error en solicitud {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = detail, Detail = detail,
            Type = $"https://httpstatuses.com/{status}", Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: ct);
        return true;
    }
}
