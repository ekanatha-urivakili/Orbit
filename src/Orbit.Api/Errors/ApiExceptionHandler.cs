using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orbit.Application.Common;
using Orbit.Domain.Common;

namespace Orbit.Api.Errors;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, type, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "/problems/validation", "Validation failed"),
            DomainException => (StatusCodes.Status400BadRequest, "/problems/domain-rule", "A domain rule was violated"),
            AuthenticationException => (StatusCodes.Status401Unauthorized, "/problems/authentication", "Authentication failed"),
            AccessDeniedException => (StatusCodes.Status403Forbidden, "/problems/forbidden", "Access denied"),
            ConflictException => (StatusCodes.Status409Conflict, "/problems/conflict", "The request conflicts with current state"),
            NotFoundException => (StatusCodes.Status404NotFound, "/problems/not-found", "Resource not found"),
            ConcurrencyException or DbUpdateConcurrencyException =>
                (StatusCodes.Status412PreconditionFailed, "/problems/precondition", "The resource has changed"),
            DbUpdateException => (StatusCodes.Status409Conflict, "/problems/conflict", "The change conflicts with existing data"),
            _ => (StatusCodes.Status500InternalServerError, "/problems/internal", "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request failure. Trace id: {TraceId}", httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
        };
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
