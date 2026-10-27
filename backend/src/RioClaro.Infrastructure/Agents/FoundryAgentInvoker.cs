extern alias AzureIdentity;

using System.ClientModel;
using System.Diagnostics;
using Azure;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RioClaro.Application.Agents;
using AuthenticationFailedException = AzureIdentity::Azure.Identity.AuthenticationFailedException;

namespace RioClaro.Infrastructure.Agents;

internal interface IFoundryAgentInvoker
{
    Task<string> InvokeAsync(
        string? agentName,
        string? agentVersion,
        string prompt,
        CancellationToken cancellationToken);
}

internal interface IFoundryAgentVersionProbe
{
    Task<string> GetStatusAsync(
        Uri projectEndpoint,
        string agentName,
        string agentVersion,
        CancellationToken cancellationToken);
}

internal sealed class FoundryAgentInvoker(
    IOptions<FoundryOptions> foundryOptions,
    ILogger<FoundryAgentInvoker> logger) : IFoundryAgentInvoker
{
    public async Task<string> InvokeAsync(
        string? agentName,
        string? agentVersion,
        string prompt,
        CancellationToken cancellationToken)
    {
        var settings = foundryOptions.Value;
        if (!settings.HasProjectEndpoint ||
            string.IsNullOrWhiteSpace(agentName) ||
            string.IsNullOrWhiteSpace(agentVersion) ||
            !settings.HasValidLimits)
        {
            throw new AgentNotConfiguredException();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
        var elapsed = Stopwatch.StartNew();
        var correlationId = Activity.Current?.TraceId.ToString() ?? "unavailable";

        try
        {
            var projectClient = new AIProjectClient(
                new Uri(settings.ProjectEndpoint!),
                new AzureIdentity::Azure.Identity.DefaultAzureCredential());
            var agent = projectClient.AsAIAgent(new AgentReference(agentName, agentVersion));
            var result = await agent.RunAsync(prompt, cancellationToken: timeout.Token);
            var response = result.Text;
            if (string.IsNullOrWhiteSpace(response))
            {
                logger.LogError(
                    "Foundry agent returned an empty response. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=false",
                    correlationId,
                    elapsed.ElapsedMilliseconds);
                throw new AgentInvalidResponseException();
            }

            if (response.Length > settings.MaxResponseCharacters)
            {
                logger.LogError(
                    "Foundry agent response exceeded the configured limit. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=false",
                    correlationId,
                    elapsed.ElapsedMilliseconds);
                throw new AgentResponseTooLargeException();
            }

            logger.LogInformation(
                "Foundry agent request completed. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=true",
                correlationId,
                elapsed.ElapsedMilliseconds);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Foundry agent request was cancelled. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=false",
                correlationId,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning(
                "Foundry agent request timed out. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=false; ErrorType={ErrorType}",
                correlationId,
                elapsed.ElapsedMilliseconds,
                exception.GetType().Name);
            throw new AgentTimeoutException();
        }
        catch (Exception exception) when (IsProviderException(exception))
        {
            logger.LogError(
                "Foundry agent request failed. CorrelationId={CorrelationId}; DurationMs={DurationMs}; Success=false; ErrorType={ErrorType}",
                correlationId,
                elapsed.ElapsedMilliseconds,
                exception.GetType().Name);
            throw new AgentProviderException();
        }
    }

    private static bool IsProviderException(Exception exception) =>
        exception is RequestFailedException or
            AuthenticationFailedException or
            ClientResultException or
            HttpRequestException;
}

internal sealed class FoundryAgentVersionProbe : IFoundryAgentVersionProbe
{
    public async Task<string> GetStatusAsync(
        Uri projectEndpoint,
        string agentName,
        string agentVersion,
        CancellationToken cancellationToken)
    {
        var client = new AgentAdministrationClient(
            projectEndpoint,
            new AzureIdentity::Azure.Identity.DefaultAzureCredential());
        await client.GetAgentVersionAsync(
            agentName,
            agentVersion,
            cancellationToken);
        return "Healthy";
    }
}
