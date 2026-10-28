using Faunex.Application.Tenancy;

namespace Faunex.Web.Tenancy;

public sealed class TenantHostGateway(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IHttpClientFactory clients)
    {
        var host = MarketplaceHosts.Normalize(context.Request.Host.Host);
        if (MarketplaceHosts.IsPlatform(host))
        {
            await next(context);
            return;
        }
        if (host is null || !MarketplaceHosts.IsTenantDomain(host))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Marketplace not found.");
            return;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/marketplace/context?host={Uri.EscapeDataString(host)}");
            request.Headers.Add(MarketplaceHosts.OriginHeader, host);
            using var response = await clients.CreateClient("FaunexApi").SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = response.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 : 503;
                await context.Response.WriteAsync("Marketplace not found or temporarily unavailable.");
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested))
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Marketplace temporarily unavailable.");
            return;
        }
        await next(context);
    }
}
