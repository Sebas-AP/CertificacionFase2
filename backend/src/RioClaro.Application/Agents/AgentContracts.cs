using RioClaro.Application.Reports;
using RioClaro.Application.Inventory;

namespace RioClaro.Application.Agents;

public sealed record AgentChatResponse(
    string Reply,
    IReadOnlyList<AgentSource> Sources);

public sealed record AgentSource(string Title, string? Uri);

public sealed record AgentInvoiceData(
    Guid InvoiceId,
    string? SupplierName,
    string? SupplierTaxId,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? CurrencyCode,
    decimal? Subtotal,
    decimal? Taxes,
    decimal? Total,
    string Status,
    IReadOnlyDictionary<string, double?> FieldConfidence,
    IReadOnlyList<string> ValidationWarnings);

public sealed record InvoiceDuplicateMatch(
    Guid InvoiceId,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    decimal? Total,
    string? CurrencyCode,
    string MatchType,
    IReadOnlyList<string> MatchedRules,
    decimal? Score);

public sealed record AgentInvoiceSummary(
    Guid InvoiceId,
    string? SupplierName,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    decimal? Total,
    string? CurrencyCode,
    string Status);

public sealed record AgentInvoicePageResponse(
    IReadOnlyList<AgentInvoiceSummary> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record InvoiceDuplicateMatchesResponse(
    Guid InvoiceId,
    IReadOnlyList<InvoiceDuplicateMatch> Matches,
    bool Truncated);

public sealed record InvoiceAgentContext(
    AgentInvoiceData Invoice,
    InvoiceDuplicateMatchesResponse DuplicateMatches);

public sealed record InvoiceExplanationResponse(
    Guid InvoiceId,
    string Explanation,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<InvoiceDuplicateMatch> DuplicateMatches);

public sealed record AgentServiceHealth(string Status);

public sealed record AgentsHealthResponse(
    AgentServiceHealth BusinessAgent,
    AgentServiceHealth InvoiceAgent,
    string? ModelDeployment,
    DateTimeOffset CheckedAtUtc);

public sealed record IntegrationHealthResponse(
    bool BlobStorageConfigured,
    bool DocumentIntelligenceConfigured,
    AgentsHealthResponse Agents);

public interface IBusinessAgentClient
{
    bool IsConfigured { get; }

    Task<AgentChatResponse> SendMessageAsync(
        string message,
        CancellationToken cancellationToken = default);
}

public interface IInvoiceAgentClient
{
    bool IsConfigured { get; }

    Task<string> ExplainAsync(
        InvoiceAgentContext context,
        CancellationToken cancellationToken = default);
}

public interface IAgentHealthService
{
    Task<AgentsHealthResponse> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAgentReadOnlyDataService
{
    Task<AgentInvoicePageResponse> GetInvoicesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AgentInvoiceData?> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceDuplicateMatchesResponse?> GetDuplicateMatchesAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceAgentContext?> GetInvoiceContextAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<ManagementReportResponse> GetIndicatorsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<InventoryPageResponse> GetInventoryAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IntegrationHealthResponse> GetIntegrationHealthAsync(
        CancellationToken cancellationToken = default);
}
