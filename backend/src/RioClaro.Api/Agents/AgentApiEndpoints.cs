using RioClaro.Application.Agents;

namespace RioClaro.Api.Agents;

internal static class AgentApiEndpoints
{
    public static void MapAgentApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/invoices/{invoiceId:guid}/explain", async (
                Guid invoiceId,
                IInvoiceAgentClient agentClient,
                IAgentReadOnlyDataService dataService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var context = await dataService.GetInvoiceContextAsync(
                    invoiceId,
                    cancellationToken);
                if (context is null)
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

                if (!agentClient.IsConfigured)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_not_configured",
                        StatusCodes.Status503ServiceUnavailable);
                }

                try
                {
                    var explanation = await agentClient.ExplainAsync(context, cancellationToken);
                    return Results.Ok(new InvoiceExplanationResponse(
                        invoiceId,
                        explanation,
                        context.Invoice.ValidationWarnings,
                        context.DuplicateMatches.Matches));
                }
                catch (AgentNotConfiguredException)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_not_configured",
                        StatusCodes.Status503ServiceUnavailable);
                }
                catch (AgentTimeoutException)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_timeout",
                        StatusCodes.Status504GatewayTimeout);
                }
                catch (AgentProviderException)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_unavailable",
                        StatusCodes.Status503ServiceUnavailable);
                }
                catch (AgentInvalidResponseException)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_invalid_response",
                        StatusCodes.Status502BadGateway);
                }
                catch (AgentResponseTooLargeException)
                {
                    return AgentUnavailable(
                        httpContext,
                        "invoice_agent_response_too_large",
                        StatusCodes.Status502BadGateway);
                }
            })
            .WithName("ExplainInvoice")
            .WithTags("Invoices")
            .Produces<InvoiceExplanationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/agents/health", async (
                IAgentHealthService healthService,
                CancellationToken cancellationToken) =>
                Results.Ok(await healthService.GetAsync(cancellationToken)))
            .WithName("GetAgentsHealth")
            .WithTags("Agents")
            .Produces<AgentsHealthResponse>();
    }

    private static IResult AgentUnavailable(
        HttpContext httpContext,
        string code,
        int statusCode) =>
        Results.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status504GatewayTimeout
                ? "La solicitud al agente excedió el tiempo límite."
                : "El agente no pudo completar la solicitud.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });
}
