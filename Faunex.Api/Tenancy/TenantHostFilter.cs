using Faunex.Application.Auth;
using Faunex.Application.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Faunex.Api.Tenancy;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InfrastructureEndpointAttribute : Attribute { }

public sealed class TenantHostFilter(TenantRequestContext origin) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<InfrastructureEndpointAttribute>().Any())
        {
            await next();
            return;
        }
        if (origin.Host is null || (!origin.IsPlatform && await origin.TenantIdAsync(context.HttpContext.RequestAborted) is null))
        {
            context.Result = new NotFoundObjectResult(new { error = "Marketplace not found or unavailable." });
            return;
        }
        if (context.ActionArguments.TryGetValue("host", out var queryHost) && queryHost is string requestedHost
            && MarketplaceHosts.Normalize(requestedHost) != origin.Host)
        {
            context.Result = new BadRequestObjectResult(new { error = "Marketplace hostname does not match this request." });
            return;
        }
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true
            && !await origin.AllowsAccountAsync(IdentityTenantMapping.ResolveTenantId(user), IdentityTenantMapping.ResolveIsPlatformAdmin(user),
                user.IsInRole(FaunexRoles.TenantAdmin), context.HttpContext.RequestAborted))
        {
            context.Result = new ForbidResult();
            return;
        }
        await next();
    }
}
