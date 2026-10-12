using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RioClaro.Infrastructure.Persistence;

namespace RioClaro.Api.Tests;

public sealed class RioClaroApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _environment;
    private readonly string _frontendOrigin;

    public RioClaroApiFactory(
        string environment = "Development",
        string frontendOrigin = "http://localhost:3000")
    {
        _environment = environment;
        _frontendOrigin = frontendOrigin;
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
                ["Frontend:Origin"] = _frontendOrigin
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RioClaroDbContext>>();
            services.RemoveAll<RioClaroDbContext>();
            services.AddDbContext<RioClaroDbContext>(
                options => options.UseSqlite(_connection));
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
