using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ToledoMessage.Filters;

/// <inheritdoc />
/// <summary>
/// Converts UnauthorizedAccessException (e.g. from GetUserId() when claims are missing)
/// into a 401 response instead of letting it bubble up as a 500.
/// </summary>
public class UnauthorizedExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // ReSharper disable once InvertIf
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = new UnauthorizedObjectResult(context.Exception.Message);
            context.ExceptionHandled = true;
        }
    }
}
