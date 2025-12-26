using Faunex.Web.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Faunex.Web.Api;

public sealed class FaunexApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
{
    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TBody, TResponse>(string path, TBody body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
    }

    public async Task PostAsync<TBody>(string path, TBody body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request)
    {
        var token = await tokenStore.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
