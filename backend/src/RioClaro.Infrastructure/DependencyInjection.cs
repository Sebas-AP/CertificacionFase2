using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Application;
using RioClaro.Application.Agents;
using RioClaro.Application.Invoices;
using RioClaro.Application.Inventory;
using RioClaro.Application.Reports;
using RioClaro.Infrastructure.Agents;
using RioClaro.Infrastructure.Invoices;
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
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();
        services.AddScoped<IManagementReportService, ManagementReportService>();
        services.AddScoped<IInvoiceReviewService, InvoiceReviewService>();
        services.AddScoped<IInvoiceBlobStore, AzureInvoiceBlobStore>();
        services.AddScoped<IInvoiceDocumentExtractor, AzureInvoiceDocumentExtractor>();
        services.AddScoped<IInvoiceUploadService, InvoiceUploadService>();
        services.AddScoped<IInvoiceRetentionService, InvoiceRetentionService>();
        services.AddOptions<FoundryOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ProjectEndpoint = configuration["Foundry:ProjectEndpoint"];
                options.BusinessAgentName = configuration["Foundry:BusinessAgentName"] ??
                                            options.BusinessAgentName;
                options.BusinessAgentVersion = configuration["Foundry:BusinessAgentVersion"];
                options.InvoiceAgentName = configuration["Foundry:InvoiceAgentName"] ??
                                           options.InvoiceAgentName;
                options.InvoiceAgentVersion = configuration["Foundry:InvoiceAgentVersion"];
                options.ModelDeploymentName = configuration["Foundry:ModelDeploymentName"];
                if (int.TryParse(
                        configuration["Foundry:RequestTimeoutSeconds"],
                        out var timeoutSeconds))
                {
                    options.RequestTimeoutSeconds = timeoutSeconds;
                }

                if (int.TryParse(
                        configuration["Foundry:MaxResponseCharacters"],
                        out var maxResponseCharacters))
                {
                    options.MaxResponseCharacters = maxResponseCharacters;
                }
            });
        services.AddScoped<IFoundryAgentInvoker, FoundryAgentInvoker>();
        services.AddScoped<IFoundryAgentVersionProbe, FoundryAgentVersionProbe>();
        services.AddScoped<IBusinessAgentClient, FoundryBusinessAgentClient>();
        services.AddScoped<IInvoiceAgentClient, FoundryInvoiceAgentClient>();
        services.AddScoped<IAgentHealthService, FoundryAgentHealthService>();
        services.AddScoped<IAgentReadOnlyDataService, AgentReadOnlyDataService>();

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
