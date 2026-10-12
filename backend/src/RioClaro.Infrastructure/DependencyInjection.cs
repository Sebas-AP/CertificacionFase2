using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Application;
using RioClaro.Infrastructure.Persistence;

namespace RioClaro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<RioClaroDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("RioClaro");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Falta configurar ConnectionStrings:RioClaro para Azure SQL.");
            }

            options.UseSqlServer(connectionString);
        });
        services.AddSingleton<ICurrentUserContext, DemoCurrentUserContext>();
        services.AddScoped<DemoDatabaseInitializer>();

        return services;
    }

    public static async Task InitializeDemoDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DemoDatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
