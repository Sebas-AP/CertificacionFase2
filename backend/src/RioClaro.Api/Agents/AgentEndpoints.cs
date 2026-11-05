using System.Globalization;
using RioClaro.Application.Agents;
using RioClaro.Application.Inventory;
using RioClaro.Application.Reports;

namespace RioClaro.Api.Agents;

internal static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentToolEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/agent-tools/v1")
            .WithTags("Agent tools")
            .AddEndpointFilter<AgentToolAuthorizationFilter>();

        group.MapGet("/invoices", async (
                int? page,
                int? pageSize,
                IAgentReadOnlyDataService dataService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var requestedPage = page ?? 1;
                var requestedPageSize = pageSize ?? 25;
                if (requestedPage < 1 || requestedPageSize is < 1 or > 50 ||
                    requestedPage > int.MaxValue / requestedPageSize)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Los parámetros de facturas no son válidos.",
                        detail: "Use page >= 1 y pageSize entre 1 y 50.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "invalid_invoice_query",
                            ["traceId"] = httpContext.TraceIdentifier
                        });
                }

                return Results.Ok(await dataService.GetInvoicesAsync(
                    requestedPage,
                    requestedPageSize,
                    cancellationToken));
            })
            .WithName("GetAgentInvoices")
            .Produces<AgentInvoicePageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/invoices/{invoiceId:guid}", async (
                Guid invoiceId,
                IAgentReadOnlyDataService dataService,
                CancellationToken cancellationToken) =>
            {
                var invoice = await dataService.GetInvoiceAsync(invoiceId, cancellationToken);
                return invoice is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Factura no encontrada.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "invoice_not_found"
                        })
                    : Results.Ok(invoice);
            })
            .WithName("GetAgentInvoice")
            .Produces<AgentInvoiceData>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/invoices/{invoiceId:guid}/duplicates", async (
                Guid invoiceId,
                IAgentReadOnlyDataService dataService,
                CancellationToken cancellationToken) =>
            {
                var matches = await dataService.GetDuplicateMatchesAsync(
                    invoiceId,
                    cancellationToken);
                return matches is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Factura no encontrada.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "invoice_not_found"
                        })
                    : Results.Ok(matches);
            })
            .WithName("GetAgentInvoiceDuplicates")
            .Produces<InvoiceDuplicateMatchesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/reports", async (
                string? fromUtc,
                string? toUtc,
                IAgentReadOnlyDataService dataService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseUtc(fromUtc, out var from) ||
                    !TryParseUtc(toUtc, out var to) ||
                    from >= to ||
                    to - from > TimeSpan.FromDays(366))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "El periodo del reporte no es válido.",
                        detail: "Indique fechas ISO 8601 UTC con un periodo máximo de 366 días.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "invalid_report_period",
                            ["traceId"] = httpContext.TraceIdentifier
                        });
                }

                return Results.Ok(await dataService.GetIndicatorsAsync(
                    from,
                    to,
                    cancellationToken));
            })
            .WithName("GetAgentManagementReport")
            .Produces<ManagementReportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/inventory", async (
                int? page,
                int? pageSize,
                string? search,
                IAgentReadOnlyDataService dataService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var requestedPage = page ?? 1;
                var requestedPageSize = pageSize ?? 25;
                if (requestedPage < 1 || requestedPageSize is < 1 or > 50 ||
                    requestedPage > int.MaxValue / requestedPageSize ||
                    (search?.Length ?? 0) > 100)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Los parámetros de inventario no son válidos.",
                        detail: "Use page >= 1, pageSize entre 1 y 50 y search de hasta 100 caracteres.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "invalid_inventory_query",
                            ["traceId"] = httpContext.TraceIdentifier
                        });
                }

                return Results.Ok(await dataService.GetInventoryAsync(
                    requestedPage,
                    requestedPageSize,
                    search,
                    cancellationToken));
            })
            .WithName("GetAgentInventory")
            .Produces<InventoryPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/integrations/health", async (
                IAgentReadOnlyDataService dataService,
                IAgentHealthService healthService,
                CancellationToken cancellationToken) =>
            {
                var integrationHealth = await dataService.GetIntegrationHealthAsync(cancellationToken);
                var agentsHealth = await healthService.GetAsync(cancellationToken);
                return Results.Ok(integrationHealth with { Agents = agentsHealth });
            })
            .WithName("GetAgentIntegrationHealth")
            .Produces<IntegrationHealthResponse>();

        return group;
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset parsed)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed) &&
            parsed.Offset == TimeSpan.Zero)
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
