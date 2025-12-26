using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Aspire AppHost is DEV-ONLY orchestration.
// Production orchestration remains Docker-based and is intentionally not affected here.

var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImage("postgres")
    .WithEnvironment("POSTGRES_USER", "postgres")
    .WithEnvironment("POSTGRES_DB", "stormbird_dev")
    .WithDataVolume("faunex-postgres-data");

// IMPORTANT: Aspire resource names must use letters/digits/hyphens.
var database = postgres.AddDatabase("stormbird-dev");

var api = builder.AddProject<Faunex_Api>("faunex-api")
    .WithReference(database);

var web = builder.AddProject<Faunex_Web>("faunex-web")
    .WaitFor(api)
    // Web reads ApiSettings:BaseUrl; inject it from the API's http endpoint.
    .WithEnvironment("ApiSettings__BaseUrl", api.GetEndpoint("http"));

builder.Build().Run();
