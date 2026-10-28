using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Auth;

// Serialize administrator checks with the writes they protect, including role/stamp changes.
public sealed class IdentityMutationTransactionFilter(ApplicationIdentityDbContext identityDb) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await using var transaction = await identityDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, context.HttpContext.RequestAborted);
        ActionExecutedContext? result = null;
        try
        {
            result = await next();
            if (result.Exception is not null)
            {
                if (IsSerializationConflict(result.Exception))
                {
                    result.ExceptionHandled = true;
                    result.Result = RetryConflict();
                }
                return;
            }

            if (((result.Result as IStatusCodeActionResult)?.StatusCode ?? 200) < 400)
            {
                await transaction.CommitAsync(context.HttpContext.RequestAborted);
            }
        }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            if (result is null) context.Result = RetryConflict();
            else result.Result = RetryConflict();
        }
    }

    private static bool IsSerializationConflict(Exception ex) =>
        ex is DbException { SqlState: "40001" } || (ex.InnerException is not null && IsSerializationConflict(ex.InnerException));

    private static ConflictObjectResult RetryConflict() =>
        new(new { error = "Another administrator changed these accounts. Refresh and try again." });
}
