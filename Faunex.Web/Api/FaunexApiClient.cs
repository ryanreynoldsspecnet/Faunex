using Faunex.Web.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Faunex.Application.Tenancy;

namespace Faunex.Web.Api;

public sealed class FaunexApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore, NavigationManager navigation)
{
    public async Task<T?> GetPublicAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(MarketplaceHosts.OriginHeader, navigation.ToAbsoluteUri(navigation.Uri).IdnHost);
        using var response = await httpClientFactory.CreateClient("FaunexApi").SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    public async Task<byte[]> GetFileBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        await ApplyAuthAsync(request);
        using var response = await httpClientFactory.CreateClient("FaunexApi").SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length > 10 * 1024 * 1024) throw new InvalidOperationException("File exceeds the download limit.");
        return bytes;
    }

    public async Task<T?> UploadFileAsync<T>(string path, Stream content, string name, string type, Guid revision,
        bool isPhoto, int? documentType, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "File", name);
        form.Add(new StringContent(revision.ToString()), "Revision");
        form.Add(new StringContent(isPhoto ? "true" : "false"), "IsPhoto");
        if (documentType.HasValue) form.Add(new StringContent(documentType.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "DocumentType");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        await ApplyAuthAsync(request);
        using var response = await httpClientFactory.CreateClient("FaunexApi").SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    public async Task<T?> DeleteFileAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        await ApplyAuthAsync(request);
        using var response = await httpClientFactory.CreateClient("FaunexApi").SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

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
        await EnsureSuccessAsync(response, cancellationToken);

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
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TResponse?> PutAsync<TBody, TResponse>(string path, TBody body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body)
        };

        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
    }

    public async Task PutAsync<TBody>(string path, TBody body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body)
        };

        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("FaunexApi");

        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        await ApplyAuthAsync(request);

        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await tokenStore.ClearAsync();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ExtractErrorMessage(body);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => "The request could not be completed. Please check the details and try again.",
                System.Net.HttpStatusCode.Unauthorized => "Invalid login details or your session has expired.",
                System.Net.HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
                System.Net.HttpStatusCode.Conflict => "This record already exists or conflicts with existing data.",
                _ => $"Request failed with status {(int)response.StatusCode}."
            };
        }

        throw new InvalidOperationException(message);
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors
                    .EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                return messages.Length == 0 ? null : string.Join(" ", messages);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request)
    {
        request.Headers.Add(MarketplaceHosts.OriginHeader, navigation.ToAbsoluteUri(navigation.Uri).IdnHost);
        var token = await tokenStore.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
