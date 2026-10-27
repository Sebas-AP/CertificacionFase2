using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RioClaro.Api.Agents;
using RioClaro.Application;
using RioClaro.Application.Agents;
using RioClaro.Application.Invoices;
using RioClaro.Application.Inventory;
using RioClaro.Application.Reports;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure;
using RioClaro.Infrastructure.Invoices;
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

builder.Services.AddSingleton<IValidateOptions<PdfUploadOptions>, InvoiceServiceOptionsValidator>();
builder.Services.AddSingleton<AzureInvoiceOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<AzureStorageOptions>>(
    serviceProvider => serviceProvider.GetRequiredService<AzureInvoiceOptionsValidator>());
builder.Services.AddSingleton<IValidateOptions<AzureDocumentIntelligenceOptions>>(
    serviceProvider => serviceProvider.GetRequiredService<AzureInvoiceOptionsValidator>());
builder.Services.AddOptions<PdfUploadOptions>()
    .BindConfiguration(PdfUploadOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddOptions<AzureStorageOptions>()
    .BindConfiguration(AzureStorageOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddOptions<AzureDocumentIntelligenceOptions>()
    .BindConfiguration(AzureDocumentIntelligenceOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        builder.Configuration.GetValue<long>("PdfUpload:MaxFileSizeBytes", 20_971_520) + 1_048_576;
    options.MemoryBufferThreshold = 64 * 1024;
    options.ValueCountLimit = 1;
});
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<DemoDatabaseInitializationHostedService>();
builder.Services.AddHostedService<InvoiceRetentionBackgroundService>();
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
    options.AddSecurityDefinition("AgentToolBearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "opaque agent-tools key",
        Description = "Bearer key configured in AgentTools:ApiKey."
    });
    options.OperationFilter<AgentToolOpenApiOperationFilter>();
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
app.UseStatusCodePages();
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
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var result = await inventoryQuery.GetByIdAsync(
            currentUser.OrganizationId,
            productId,
            cancellationToken);
        return result is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Producto no encontrado.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "inventory_item_not_found",
                    ["traceId"] = httpContext.TraceIdentifier
                })
            : Results.Ok(result);
    })
    .WithName("GetInventoryItem")
    .WithTags("Inventory")
    .Produces<InventoryItemResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/reportes", async (
        string? fromUtc,
        string? toUtc,
        ICurrentUserContext currentUser,
        IManagementReportService reportService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        if (!TryParseUtc(fromUtc, out var from) ||
            !TryParseUtc(toUtc, out var to) ||
            from >= to)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "El periodo del reporte no es válido.",
                detail: "Indique fromUtc y toUtc como fechas ISO 8601 UTC y asegure que fromUtc sea anterior a toUtc.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_report_period",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var report = await reportService.GetAsync(
            currentUser.OrganizationId,
            from,
            to,
            cancellationToken);
        return Results.Ok(report);
    })
    .WithName("GetManagementReport")
    .WithTags("Reports")
    .Produces<ManagementReportResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/api/chat", async (
        ChatRequest request,
        IBusinessAgentClient agentClient,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "El mensaje de chat no es válido.",
                detail: "El mensaje debe contener entre 1 y 4000 caracteres.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_chat_message",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        if (request.ConversationId.HasValue)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "La conversación no está persistida.",
                    detail: "Omita conversationId; la API aún no conserva el estado conversacional.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_conversation_not_supported",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }

        if (!agentClient.IsConfigured)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "El servicio de chat no está configurado.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_not_configured",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }

        try
        {
                return Results.Ok(await agentClient.SendMessageAsync(
                    request.Message,
                    cancellationToken));
        }
        catch (AgentNotConfiguredException)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "El servicio de chat no está configurado.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_not_configured",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }
        catch (AgentTimeoutException)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "La solicitud al agente excedió el tiempo límite.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_timeout",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }
        catch (AgentProviderException)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "El servicio de chat no está disponible.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_provider_unavailable",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }
        catch (AgentInvalidResponseException)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "El agente devolvió una respuesta no válida.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_invalid_response",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }
        catch (AgentResponseTooLargeException)
        {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "La respuesta del agente excedió el límite permitido.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "chat_response_too_large",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
        }
    })
    .WithName("Chat")
    .WithTags("Chat")
    .Accepts<ChatRequest>("application/json")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapAgentApiEndpoints();
app.MapAgentToolEndpoints();

if (string.Equals(
        builder.Configuration["Application:Mode"],
        "Demo",
        StringComparison.Ordinal))
{
    app.MapPost("/api/admin/retention/run", async (
            ICurrentUserContext currentUser,
            IInvoiceRetentionService retentionService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsDemoMode)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "La ruta no está disponible.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "route_not_available",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            var result = await retentionService.RunOnceAsync(cancellationToken);
            if (result.FailedPdfCount > 0)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "La limpieza de retención terminó con errores.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "retention_run_incomplete",
                        ["deletedPdfCount"] = result.DeletedPdfCount,
                        ["failedPdfCount"] = result.FailedPdfCount,
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            return Results.Ok(result);
        })
        .WithName("RunDemoInvoiceRetention")
        .WithTags("Administration")
        .Produces<InvoiceRetentionRunResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}

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
                          new[]
                          {
                              "pending_review",
                              "approved",
                              "extraction_failed",
                              "extracted",
                              "needs_validation",
                              "duplicate"
                          }
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

app.MapPost("/api/invoices/upload", async (
        HttpRequest request,
        IInvoiceUploadService invoiceUpload,
        IOptions<PdfUploadOptions> uploadOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var maximumRequestSize = uploadOptions.Value.MaxFileSizeBytes + 1_048_576;
        if (request.ContentLength > maximumRequestSize)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "El archivo excede el tamaño máximo permitido.",
                detail: $"El máximo permitido es {uploadOptions.Value.MaxFileSizeBytes} bytes.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "pdf_too_large",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        if (!request.HasFormContentType ||
            !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Se requiere una solicitud multipart/form-data.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "multipart_form_required",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "El formulario multipart no es válido.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "invalid_multipart_form",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        if (form.Files.Count != 1 || form.Count != 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Debe enviar exactamente un archivo y ningún otro campo.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "single_file_required",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var file = form.Files[0];
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await invoiceUpload.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                httpContext.TraceIdentifier,
                cancellationToken);
            if (result.Duplicate)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "La factura ya existe.",
                    detail: result.Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "duplicate_invoice_file",
                        ["duplicateInvoiceId"] = result.DuplicateInvoiceId,
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            return Results.Created(
                $"/api/facturas/{result.Result!.Invoice.Id}",
                result.Result);
        }
        catch (InvoiceInputException exception)
        {
            return Results.Problem(
                statusCode: exception.StatusCode,
                title: exception.Title,
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = exception.Code,
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }
        catch (InvoiceDependencyException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Un servicio requerido no está disponible.",
                detail: exception.Code == "blob_storage_not_configured"
                    ? "Configure Azure Storage para habilitar la carga de facturas."
                    : "Azure Blob Storage no pudo conservar el archivo.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = exception.Code,
                    ["dependency"] = exception.Dependency,
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }
    })
    .WithName("UploadInvoice")
    .WithTags("Invoices")
    .Accepts<IFormFile>("multipart/form-data")
    .DisableAntiforgery()
    .WithMetadata(new RequestSizeLimitAttribute(21_971_520))
    .Produces<InvoiceUploadResult>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapPost("/api/invoices/{invoiceId:guid}/retry-extraction", async (
        Guid invoiceId,
        IInvoiceUploadService invoiceUpload,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await invoiceUpload.RetryExtractionAsync(
                invoiceId,
                httpContext.TraceIdentifier,
                cancellationToken);
            if (result.NotFound)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Factura no encontrada.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "invoice_not_found",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (result.Conflict is not null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "No se puede reintentar la extracción.",
                    detail: result.Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "invoice_extraction_conflict",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (result.Result is { ExtractionSucceeded: false } failedExtraction)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "No se pudo completar la extracción.",
                    detail: failedExtraction.ExtractionErrorMessage ??
                            "La factura y el PDF se conservaron; consulte la factura o vuelva a intentar más tarde.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = failedExtraction.ExtractionErrorCode ?? "document_intelligence_failed",
                        ["invoiceId"] = failedExtraction.Invoice.Id,
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            return Results.Ok(result.Result);
        }
        catch (InvoiceDependencyException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Un servicio requerido no está disponible.",
                detail: exception.Code switch
                {
                    "document_intelligence_not_configured" =>
                        "Configure Azure Document Intelligence para procesar la factura.",
                    "blob_storage_not_configured" =>
                        "Configure Azure Storage para habilitar el acceso al PDF original.",
                    _ => "No fue posible acceder al PDF original en Blob Storage."
                },
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = exception.Code,
                    ["dependency"] = exception.Dependency,
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }
    })
    .WithName("RetryInvoiceExtraction")
    .WithTags("Invoices")
    .Produces<InvoiceUploadResult>()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

static bool TryParseUtc(string? value, out DateTimeOffset result)
{
    if (DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal,
            out result) &&
        result.Offset == TimeSpan.Zero)
    {
        return true;
    }

    result = default;
    return false;
}

app.Run();

public sealed record DemoContextResponse(
    string Mode,
    DemoOrganizationResponse Organization,
    DemoUserResponse User);

public sealed record DemoOrganizationResponse(Guid Id, string Name);

public sealed record DemoUserResponse(Guid Id, string DisplayName, string Role);

public sealed record ChatRequest(string? Message, Guid? ConversationId = null);

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
