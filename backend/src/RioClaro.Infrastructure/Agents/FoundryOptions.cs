namespace RioClaro.Infrastructure.Agents;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string? ProjectEndpoint { get; set; }
    public string? BusinessAgentName { get; set; } = "rio-claro-business-agent";
    public string? BusinessAgentVersion { get; set; }
    public string? InvoiceAgentName { get; set; } = "rio-claro-invoice-agent";
    public string? InvoiceAgentVersion { get; set; }
    public string? ModelDeploymentName { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int MaxResponseCharacters { get; set; } = 12_000;

    public bool HasProjectEndpoint =>
        Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps;

    public bool IsBusinessAgentConfigured =>
        HasProjectEndpoint &&
        !string.IsNullOrWhiteSpace(BusinessAgentName) &&
        !string.IsNullOrWhiteSpace(BusinessAgentVersion);

    public bool IsInvoiceAgentConfigured =>
        HasProjectEndpoint &&
        !string.IsNullOrWhiteSpace(InvoiceAgentName) &&
        !string.IsNullOrWhiteSpace(InvoiceAgentVersion);

    public bool HasValidLimits =>
        RequestTimeoutSeconds is >= 1 and <= 120 &&
        MaxResponseCharacters is >= 1 and <= 16_000;
}
