using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RioClaro.Application.Invoices;
using RioClaro.Infrastructure.Invoices;
using RioClaro.Infrastructure.Persistence;

namespace RioClaro.Api.Tests;

public sealed class RioClaroApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _environment;
    private readonly string _frontendOrigin;
    private readonly string? _agentToolsApiKey;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public RioClaroApiFactory(
        string environment = "Development",
        string frontendOrigin = "http://localhost:3000",
        Action<IServiceCollection>? configureTestServices = null,
        string? agentToolsApiKey = null)
    {
        _environment = environment;
        _frontendOrigin = frontendOrigin;
        _configureTestServices = configureTestServices;
        _agentToolsApiKey = agentToolsApiKey;
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RioClaro"] = "Server=localhost;Database=RioClaroTests",
                ["Application:Mode"] = "Demo",
                ["Application:RequireAuthentication"] = "false",
                ["Frontend:Origin"] = _frontendOrigin,
                ["PdfUpload:AllowedContentTypes:0"] = "application/pdf",
                ["PdfUpload:MaxFileSizeBytes"] = "20971520",
                ["PdfUpload:MaxPages"] = "50",
                ["PdfUpload:RetentionDays"] = "60",
                ["PdfUpload:ProcessingTimeoutSeconds"] = "120",
                ["PdfUpload:MaxExtractionRetries"] = "3",
                ["AgentTools:ApiKey"] = _agentToolsApiKey
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RioClaroDbContext>>();
            services.RemoveAll<RioClaroDbContext>();
            services.AddDbContext<RioClaroDbContext>(
                options => options.UseSqlite(_connection));
            services.PostConfigure<PdfUploadOptions>(options =>
                options.AllowedContentTypes = ["application/pdf"]);
            _configureTestServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
