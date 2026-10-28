using Faunex.Application.Tenancy;

namespace Faunex.Web.Api;

public static class ListingMediaEndpoints
{
    public static void MapListingMedia(this WebApplication app)
    {
        app.MapGet("/media/listing-photos/{fileId:guid}", async (Guid fileId, HttpContext context, IHttpClientFactory clients) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var host = MarketplaceHosts.Normalize(context.Request.Host.Host);
            if (!MarketplaceHosts.IsTenantDomain(host)) { context.Response.StatusCode = 404; return; }
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/marketplace/photos/{fileId}");
            request.Headers.Add(MarketplaceHosts.OriginHeader, host);
            try
            {
                using var response = await clients.CreateClient("FaunexApi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                var type = response.Content.Headers.ContentType?.MediaType;
                if (!response.IsSuccessStatusCode || type is not ("image/png" or "image/jpeg"))
                {
                    context.Response.StatusCode = response.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 : 503;
                    return;
                }
                context.Response.ContentType = type;
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (HttpRequestException) when (!context.Response.HasStarted) { context.Response.StatusCode = 503; }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
            { context.Response.StatusCode = 503; }
        });
    }
}
