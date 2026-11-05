using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RioClaro.Api.Agents;

internal sealed class AgentToolAuthorizationFilter(
    IConfiguration configuration,
    ILogger<AgentToolAuthorizationFilter> logger) : IEndpointFilter
{
    private const string AuthorizationScheme = "Bearer ";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ??
                            context.HttpContext.TraceIdentifier;
        var configuredKey = configuration["AgentTools:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) || configuredKey.Length < 32)
        {
            logger.LogWarning(
                "Foundry tool authorization is not configured. CorrelationId={CorrelationId}; Success=false",
                correlationId);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Los servicios de herramientas del agente no están configurados.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "agent_tools_not_configured",
                    ["traceId"] = context.HttpContext.TraceIdentifier
                });
        }

        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(AuthorizationScheme, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(context.HttpContext, logger, correlationId);
        }

        var suppliedKey = authorization[AuthorizationScheme.Length..].Trim();
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        if (suppliedBytes.Length != configuredBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes))
        {
            return Unauthorized(context.HttpContext, logger, correlationId);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next(context);
            var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ??
                             StatusCodes.Status200OK;
            logger.LogInformation(
                "Foundry tool request completed. CorrelationId={CorrelationId}; Operation={Operation}; DurationMs={DurationMs}; Success={Success}",
                correlationId,
                context.HttpContext.GetEndpoint()?.DisplayName ?? "unknown",
                stopwatch.ElapsedMilliseconds,
                statusCode < StatusCodes.Status400BadRequest);
            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Foundry tool request failed. CorrelationId={CorrelationId}; Operation={Operation}; DurationMs={DurationMs}; Success=false; ErrorType={ErrorType}",
                correlationId,
                context.HttpContext.GetEndpoint()?.DisplayName ?? "unknown",
                stopwatch.ElapsedMilliseconds,
                exception.GetType().Name);
            throw;
        }
    }

    private static IResult Unauthorized(
        HttpContext context,
        ILogger<AgentToolAuthorizationFilter> logger,
        string correlationId)
    {
        logger.LogWarning(
            "Foundry tool authorization failed. CorrelationId={CorrelationId}; Operation={Operation}; Success=false",
            correlationId,
            context.GetEndpoint()?.DisplayName ?? "unknown");
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Se requiere autorización para usar esta herramienta.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "agent_tool_unauthorized",
                ["traceId"] = context.TraceIdentifier
            });
    }
}
