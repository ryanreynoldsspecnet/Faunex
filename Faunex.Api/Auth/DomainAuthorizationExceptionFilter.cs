using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Faunex.Api.Auth;

public sealed class DomainAuthorizationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
                ? new ForbidResult() : new ChallengeResult();
            context.ExceptionHandled = true;
        }
    }
}
