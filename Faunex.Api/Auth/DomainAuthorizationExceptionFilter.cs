using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Faunex.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Faunex.Api.Files;

namespace Faunex.Api.Auth;

public sealed class DomainAuthorizationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is FileSecurityException or FileServiceUnavailableException)
        {
            context.Result = new ObjectResult(new { error = context.Exception.Message })
            { StatusCode = context.Exception is FileSecurityException ? 400 : 503 };
            context.ExceptionHandled = true;
            return;
        }
        if (context.Exception is ListingValidationException)
        {
            context.Result = new BadRequestObjectResult(new { error = context.Exception.Message });
            context.ExceptionHandled = true;
        }
        else if (context.Exception is ListingEditConflictException or DbUpdateConcurrencyException || IsBidConflict(context.Exception))
        {
            context.Result = new ConflictObjectResult(new { error = context.Exception is ListingEditConflictException
                ? context.Exception.Message : "This record changed while you were saving. Reload it and try again." });
            context.ExceptionHandled = true;
        }
        else if (context.Exception is KeyNotFoundException)
        {
            context.Result = new NotFoundObjectResult(new { error = context.Exception.Message });
            context.ExceptionHandled = true;
        }
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
                ? new ForbidResult() : new ChallengeResult();
            context.ExceptionHandled = true;
        }
    }
    private static bool IsBidConflict(Exception ex) => ex is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_Bids_AuctionId_BidNumber" }
        || (ex.InnerException is not null && IsBidConflict(ex.InnerException));
}
