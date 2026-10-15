using Microsoft.OpenApi.Models;
using RioClaro.Application;
using RioClaro.Application.Invoices;
using RioClaro.Application.Inventory;
using RioClaro.Domain.Entities;
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
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (context.ProblemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Title = "Error interno del servidor.";
            context.ProblemDetails.Detail = null;
        }
    };
});
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

app.UseExceptionHandler();
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

app.MapGet("/api/inventario", async (
        int? page,
        int? pageSize,
        string? buscar,
        ICurrentUserContext currentUser,
        IInventoryQueryService inventoryQuery,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var pageNumber = page ?? 1;
        var requestedPageSize = pageSize ?? 25;
        if (pageNumber < 1 || requestedPageSize < 1 || requestedPageSize > 100 ||
            pageNumber > int.MaxValue / requestedPageSize ||
            (buscar?.Length ?? 0) > 100)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Parámetros de inventario inválidos.",
                detail: "Use page >= 1, pageSize entre 1 y 100 y buscar de hasta 100 caracteres.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_inventory_query",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var result = await inventoryQuery.GetPageAsync(
            currentUser.OrganizationId,
            pageNumber,
            requestedPageSize,
            buscar,
            cancellationToken);
        return Results.Ok(result);
    })
    .WithName("GetInventory")
    .WithTags("Inventory")
    .Produces<InventoryPageResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/api/inventario/{productId:guid}", async (
        Guid productId,
        ICurrentUserContext currentUser,
        IInventoryQueryService inventoryQuery,
        CancellationToken cancellationToken) =>
    {
        var result = await inventoryQuery.GetByIdAsync(
            currentUser.OrganizationId,
            productId,
            cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetInventoryItem")
    .WithTags("Inventory")
    .Produces<InventoryItemResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/facturas", async (
        int? page,
        int? pageSize,
        string? estado,
        ICurrentUserContext currentUser,
        IInvoiceReviewService invoiceService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var pageNumber = page ?? 1;
        var requestedPageSize = pageSize ?? 25;
        var validStatus = string.IsNullOrWhiteSpace(estado) ||
                          new[] { "pending_review", "approved", "extraction_failed" }
                              .Contains(estado.Trim(), StringComparer.OrdinalIgnoreCase);
        if (pageNumber < 1 || requestedPageSize < 1 || requestedPageSize > 100 ||
            pageNumber > int.MaxValue / requestedPageSize || !validStatus)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Parámetros de facturas inválidos.",
                detail: "Use page >= 1, pageSize entre 1 y 100 y un estado reconocido.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_invoice_query",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var result = await invoiceService.GetPageAsync(
            currentUser.OrganizationId,
            pageNumber,
            requestedPageSize,
            estado,
            cancellationToken);
        return Results.Ok(result);
    })
    .WithName("GetInvoices")
    .WithTags("Invoices")
    .Produces<InvoicePageResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/api/facturas/{invoiceId:guid}", async (
        Guid invoiceId,
        ICurrentUserContext currentUser,
        IInvoiceReviewService invoiceService,
        CancellationToken cancellationToken) =>
    {
        var result = await invoiceService.GetByIdAsync(
            currentUser.OrganizationId,
            invoiceId,
            cancellationToken);
        return result is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Factura no encontrada.",
                extensions: new Dictionary<string, object?> { ["code"] = "invoice_not_found" })
            : Results.Ok(result);
    })
    .WithName("GetInvoice")
    .WithTags("Invoices")
    .Produces<InvoiceResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapPut("/api/facturas/{invoiceId:guid}/revision", async (
        Guid invoiceId,
        ReviewInvoiceRequest request,
        ICurrentUserContext currentUser,
        IInvoiceReviewService invoiceService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var currencyIsValid = request.CurrencyCode is null ||
                              (request.CurrencyCode.Length == 3 &&
                               request.CurrencyCode.All(char.IsAsciiLetter));
        var moneyIsValid = new[] { request.Subtotal, request.TaxAmount, request.Total }
            .All(IsValidMoney);
        if (request.InvoiceNumber?.Length > 100 ||
            request.Reason?.Length > 500 ||
            !currencyIsValid ||
            !moneyIsValid)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "La revisión contiene valores inválidos.",
                detail: "Revise longitudes, moneda ISO de tres letras e importes no negativos con hasta cuatro decimales.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_invoice_review",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var result = await invoiceService.ReviewAsync(
            currentUser.OrganizationId,
            currentUser.UserId,
            invoiceId,
            request,
            cancellationToken);
        if (result.NotFound)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Factura no encontrada.",
                extensions: new Dictionary<string, object?> { ["code"] = "invoice_not_found" });
        }

        return result.Conflict is not null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto en el estado de la factura.",
                detail: result.Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = "invoice_state_conflict" })
            : Results.Ok(result.Invoice);
    })
    .WithName("ReviewInvoice")
    .WithTags("Invoices")
    .Produces<InvoiceResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);

app.MapPost("/api/facturas/{invoiceId:guid}/confirmacion", async (
        Guid invoiceId,
        ICurrentUserContext currentUser,
        IInvoiceReviewService invoiceService,
        CancellationToken cancellationToken) =>
    {
        var result = await invoiceService.ApproveAsync(
            currentUser.OrganizationId,
            currentUser.UserId,
            invoiceId,
            cancellationToken);
        if (result.NotFound)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Factura no encontrada.",
                extensions: new Dictionary<string, object?> { ["code"] = "invoice_not_found" });
        }

        return result.Conflict is not null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto en el estado de la factura.",
                detail: result.Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = "invoice_state_conflict" })
            : Results.Ok(result.Invoice);
    })
    .WithName("ApproveInvoice")
    .WithTags("Invoices")
    .Produces<InvoiceResponse>()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);

static bool IsValidMoney(decimal? amount) =>
    amount is null ||
    (amount.Value >= 0 &&
     amount.Value <= 999_999_999_999_999.9999m &&
     decimal.Round(amount.Value, 4) == amount.Value);

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
