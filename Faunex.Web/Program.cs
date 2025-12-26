using Faunex.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Configuration checks
// =====================
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "Missing configuration value 'ApiSettings:BaseUrl'. " +
        "Set it in appsettings.Production.json or via the environment variable 'ApiSettings__BaseUrl'.");
}

// =====================
// Services
// =====================
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("Faunex");

builder.Services.AddHttpClient("FaunexApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// =====================
// Build app
// =====================
var app = builder.Build();

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
app.UseRouting();

// REQUIRED in .NET 10 for Blazor static web assets
app.MapStaticAssets();

// Blazor must come AFTER static assets
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.UseAntiforgery();

app.Run();
