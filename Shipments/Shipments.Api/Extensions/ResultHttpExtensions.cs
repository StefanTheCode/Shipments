using Microsoft.AspNetCore.Mvc;
using Shipments.Application.Results;

namespace Shipments.Api.Extensions;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        return MapError(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return MapError(result.Error);
    }

    public static IResult ToCreatedHttpResult<T>(this Result<T> result, string location)
    {
        if (result.IsSuccess)
        {
            return Results.Created(location, result.Value);
        }

        return MapError(result.Error);
    }

    private static IResult MapError(ResultError? error)
    {
        // fallback
        if (error is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unknown error");
        }

        var status = error.Code switch
        {
            ErrorCodes.Validation => StatusCodes.Status400BadRequest,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.Conflict => StatusCodes.Status409Conflict,
            ErrorCodes.ExternalDependency => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        // ProblemDetails standard output
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Message
        };

        if (error.Metadata is not null)
        {
            foreach (var kv in error.Metadata)
            {
                problem.Extensions[kv.Key] = kv.Value;
            }
        }

        return Results.Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            extensions: problem.Extensions);
    }
}