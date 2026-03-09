using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ToledoMessage.Data;

namespace ToledoMessage.Filters;

/// <inheritdoc />
/// <summary>
/// Global action filter that wraps every controller action in a database transaction.
/// Commits on success (2xx/3xx), rolls back on failure (4xx/5xx) or unhandled exceptions.
/// This ensures atomicity: if any part of a request fails, all DB changes are rolled back.
/// </summary>
public class TransactionFilter(ApplicationDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Skip if already inside a transaction (e.g. controller manually started one)
        if (db.Database.CurrentTransaction is not null)
        {
            await next();
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var executedContext = await next();

        // Rollback on unhandled exceptions
        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            await transaction.RollbackAsync();
            return;
        }

        // Rollback on error results (4xx/5xx)
        var statusCode = executedContext.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? 200,
            StatusCodeResult statusResult => statusResult.StatusCode,
            _ => 200
        };

        if (statusCode >= 400)
        {
            await transaction.RollbackAsync();
            return;
        }

        await transaction.CommitAsync();
    }
}
