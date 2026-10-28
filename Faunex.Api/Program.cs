using Faunex.Api.Controllers;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Faunex.Api.Auth;
using Microsoft.AspNetCore.DataProtection;
using Faunex.Api.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFaunexApiControllers();

builder.Services.Configure<SmtpEmailOptions>(options =>
{
    options.Host = builder.Configuration["FAUNEX_EMAIL_SMTP_HOST"];
    options.Port = int.TryParse(builder.Configuration["FAUNEX_EMAIL_SMTP_PORT"], out var port) ? port : 587;
    options.Username = builder.Configuration["FAUNEX_EMAIL_SMTP_USERNAME"];
    options.Password = builder.Configuration["FAUNEX_EMAIL_SMTP_PASSWORD"];
    options.EnableSsl = !bool.TryParse(builder.Configuration["FAUNEX_EMAIL_SMTP_ENABLE_SSL"], out var enableSsl) || enableSsl;
    options.FromEmail = builder.Configuration["FAUNEX_EMAIL_FROM_EMAIL"];
    options.FromName = builder.Configuration["FAUNEX_EMAIL_FROM_NAME"] ?? "Faunex";
    options.PublicBaseUrl = builder.Configuration["FAUNEX_PUBLIC_BASE_URL"] ?? "https://faunex.co.za";
});

// Share Data Protection keys across containers (Web + API)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("Faunex");

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var connectionString = GetRequiredConfiguration(cfg, env, "ConnectionStrings:DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddFaunexAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

app.Logger.LogInformation("API HOST STARTED. Environment={EnvironmentName}", app.Environment.EnvironmentName);
foreach (var url in app.Urls)
{
    app.Logger.LogInformation("API listening on {Url}", url);
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var identityDb = scope.ServiceProvider.GetRequiredService<Faunex.Api.Auth.ApplicationIdentityDbContext>();
    await identityDb.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<Faunex.Api.Auth.IdentitySeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var ex = exceptionHandlerPathFeature?.Error;

            if (ex != null)
            {
                app.Logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await Results.Problem(
                    title: "Unhandled exception",
                    detail: app.Environment.IsDevelopment() ? ex?.ToString() : null,
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: context.Request.Path)
                .ExecuteAsync(context);
        });
    });
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetRequiredConfiguration(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    string key,
    string? developmentFallback = null)
{
    var value = configuration[key];
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    if (environment.IsDevelopment() && developmentFallback is not null)
    {
        return developmentFallback;
    }

    throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
