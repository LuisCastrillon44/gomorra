using Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api;

/// <summary>Convierte las excepciones de la capa Application en respuestas ProblemDetails.</summary>
internal sealed class ManejadorExcepciones(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            NoEncontradoException => (StatusCodes.Status404NotFound, "No encontrado"),
            ConflictoException => (StatusCodes.Status409Conflict, "Conflicto"),
            ValidacionException => (StatusCodes.Status400BadRequest, "Datos inválidos"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false; // Error no esperado: lo maneja el comportamiento por defecto (500).
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = exception.Message },
        });
    }
}
