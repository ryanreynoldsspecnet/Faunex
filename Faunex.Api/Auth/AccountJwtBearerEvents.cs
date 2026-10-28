using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Faunex.Api.Auth;

public sealed class AccountJwtBearerEvents(AccountAccessService accounts) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!await accounts.ValidateSessionAsync(context.Principal, context.HttpContext.RequestAborted))
        {
            context.Fail("This session is no longer valid. Please log in again.");
        }
    }
}
