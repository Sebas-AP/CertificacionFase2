using Microsoft.OpenApi.Models;
using RioClaro.Application;
using RioClaro.Infrastructure;
using RioClaro.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

if (!string.Equals(builder.Configuration["Application:Mode"], "Demo", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Application:Mode debe configurarse como Demo.");
}

if (builder.Configuration.GetValue<bool>("Application:RequireAuthentication"))
{
    throw new InvalidOperationException(
        "Este backend de demostración no admite autenticación.");
}

var frontendOrigin = builder.Configuration["Frontend:Origin"];
if (string.IsNullOrWhiteSpace(frontendOrigin) ||
    !Uri.TryCreate(frontendOrigin, UriKind.Absolute, out var frontendUri) ||
    !string.Equals(frontendOrigin, frontendUri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Frontend:Origin debe ser un único origen absoluto, sin ruta ni barra final.");
}

builder.Services.AddInfrastructure();
builder.Services.AddHostedService<DemoDatabaseInitializationHostedService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Río Claro API",
        Version = "v1",
        Description = "API académica de demostración sin autenticación."
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOnly", policy =>
    {
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("FrontendOnly");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("Health");

app.MapGet("/api/context", (ICurrentUserContext currentUser) =>
    Results.Ok(new DemoContextResponse(
        "Demo",
        new DemoOrganizationResponse(currentUser.OrganizationId, currentUser.OrganizationName),
        new DemoUserResponse(
            currentUser.UserId,
            currentUser.UserDisplayName,
            currentUser.Role))))
    .WithName("GetDemoContext")
    .WithTags("Context")
    .Produces<DemoContextResponse>();

app.Run();

public sealed record DemoContextResponse(
    string Mode,
    DemoOrganizationResponse Organization,
    DemoUserResponse User);

public sealed record DemoOrganizationResponse(Guid Id, string Name);

public sealed record DemoUserResponse(Guid Id, string DisplayName, string Role);

public partial class Program
{
}

public sealed class DemoDatabaseInitializationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DemoDatabaseInitializationHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _serviceProvider.InitializeDemoDatabaseAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
