using Faunex.Web.Api;
using Faunex.Web.Auth;
using Faunex.Web.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Configuration checks
// =====================
var apiBaseUrlRaw = builder.Configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrlRaw))
{
    throw new InvalidOperationException(
        "Missing configuration value 'ApiSettings:BaseUrl'. " +
        "Set it in appsettings.Production.json or via the environment variable 'ApiSettings__BaseUrl'.");
}

static Uri NormalizeApiBaseUrl(string raw)
{
    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"Invalid ApiSettings:BaseUrl '{raw}'. It must be an absolute URL like 'https://api.example.com'.");
    }

    // Force API base address to be the ROOT (no /register, /auth, /api/auth, etc.).
    // This guarantees request paths like "/api/auth/register" resolve correctly.
    return new Uri(uri.GetLeftPart(UriPartial.Authority));
}

var apiBaseUrl = NormalizeApiBaseUrl(apiBaseUrlRaw);

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Information);

// =====================
// Services
// =====================
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("Faunex");

builder.Services.AddHttpClient("FaunexApi", client =>
{
    client.BaseAddress = apiBaseUrl;
});

builder.Services.AddScoped<ProtectedLocalStorage>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

builder.Services.AddScoped<FaunexApiClient>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorizationCore();

// =====================
// Build app
// =====================
var app = builder.Build();

app.Logger.LogInformation("WEB HOST STARTED. Environment={EnvironmentName}", app.Environment.EnvironmentName);
app.Logger.LogInformation("Web configured ApiSettings:BaseUrlRaw={ApiBaseUrlRaw}", apiBaseUrlRaw);
app.Logger.LogInformation("Web normalized ApiSettings:BaseUrl={ApiBaseUrl}", apiBaseUrl);

app.MapGet("/__ping", () => Results.Ok("pong"));

// =====================
// Middleware pipeline
// =====================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// HTTPS is handled by reverse proxy / Docker host
// app.UseHttpsRedirection();

app.UseStaticFiles();

// REQUIRED in .NET 10 for Blazor static web assets
app.MapStaticAssets();

app.UseRouting();

// Blazor must come AFTER static assets
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.UseAntiforgery();

app.Run();
