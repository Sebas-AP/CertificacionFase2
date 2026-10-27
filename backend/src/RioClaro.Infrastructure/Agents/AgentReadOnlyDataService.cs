using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RioClaro.Application;
using RioClaro.Application.Agents;
using RioClaro.Application.Inventory;
using RioClaro.Application.Reports;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Invoices;
using RioClaro.Infrastructure.Persistence;

namespace RioClaro.Infrastructure.Agents;

internal sealed class AgentReadOnlyDataService(
    RioClaroDbContext dbContext,
    ICurrentUserContext currentUser,
    IManagementReportService reportService,
    IInventoryQueryService inventoryQueryService,
    TimeProvider timeProvider,
    IOptions<AzureStorageOptions> storageOptions,
    IOptions<AzureDocumentIntelligenceOptions> documentIntelligenceOptions)
    : IAgentReadOnlyDataService
{
    private const int MaximumDuplicateMatches = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentInvoicePageResponse> GetInvoicesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.OrganizationId == currentUser.OrganizationId);
        var totalItems = await query.CountAsync(cancellationToken);
        var invoiceRows = await query
            .OrderByDescending(invoice => invoice.UploadedAtUtc)
            .ThenBy(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.SupplierName,
                invoice.InvoiceNumber,
                invoice.IssueDate,
                invoice.Total,
                invoice.CurrencyCode,
                invoice.Status
            })
            .ToListAsync(cancellationToken);
        var invoices = invoiceRows
            .Select(invoice => new AgentInvoiceSummary(
                invoice.Id,
                invoice.SupplierName,
                invoice.InvoiceNumber,
                invoice.IssueDate,
                invoice.Total,
                invoice.CurrencyCode,
                invoice.Status.ToString()))
            .ToList();

        return new AgentInvoicePageResponse(
            invoices,
            page,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<AgentInvoiceData?> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await FindInvoiceAsync(invoiceId, cancellationToken);
        return invoice is null ? null : MapInvoice(invoice);
    }

    public async Task<InvoiceDuplicateMatchesResponse?> GetDuplicateMatchesAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await FindInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var hasBusinessIdentity =
            !string.IsNullOrWhiteSpace(invoice.SupplierTaxIdNormalized) &&
            !string.IsNullOrWhiteSpace(invoice.InvoiceNumberNormalized) &&
            invoice.IssueDate.HasValue &&
            invoice.Total.HasValue;

        var candidates = await dbContext.Invoices
            .AsNoTracking()
            .Where(candidate =>
                candidate.OrganizationId == currentUser.OrganizationId &&
                candidate.Id != invoice.Id &&
                ((invoice.Sha256 != null && candidate.Sha256 == invoice.Sha256) ||
                 (invoice.DuplicateOfInvoiceId.HasValue &&
                  candidate.Id == invoice.DuplicateOfInvoiceId.Value) ||
                 (hasBusinessIdentity &&
                  candidate.SupplierTaxIdNormalized == invoice.SupplierTaxIdNormalized &&
                  candidate.InvoiceNumberNormalized == invoice.InvoiceNumberNormalized &&
                  candidate.IssueDate == invoice.IssueDate &&
                  candidate.Total == invoice.Total)))
            .OrderBy(candidate => candidate.Id)
            .Take(MaximumDuplicateMatches + 1)
            .ToListAsync(cancellationToken);

        var truncated = candidates.Count > MaximumDuplicateMatches;
        var matches = candidates
            .Take(MaximumDuplicateMatches)
            .Select(candidate => MapDuplicateMatch(invoice, candidate))
            .ToList();

        return new InvoiceDuplicateMatchesResponse(invoice.Id, matches, truncated);
    }

    public async Task<InvoiceAgentContext?> GetInvoiceContextAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var matches = await GetDuplicateMatchesAsync(invoiceId, cancellationToken);
        return matches is null ? null : new InvoiceAgentContext(invoice, matches);
    }

    public Task<ManagementReportResponse> GetIndicatorsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        return reportService.GetAsync(
            currentUser.OrganizationId,
            fromUtc.ToUniversalTime(),
            toUtc.ToUniversalTime(),
            cancellationToken);
    }

    public Task<InventoryPageResponse> GetInventoryAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize ||
            (search?.Length ?? 0) > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        return inventoryQueryService.GetPageAsync(
            currentUser.OrganizationId,
            page,
            pageSize,
            search,
            cancellationToken);
    }

    public Task<IntegrationHealthResponse> GetIntegrationHealthAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storage = storageOptions.Value;
        var documentIntelligence = documentIntelligenceOptions.Value;
        var storageConfigured =
            !string.IsNullOrWhiteSpace(storage.ConnectionString) ||
            Uri.TryCreate(storage.ServiceUri, UriKind.Absolute, out var storageUri) &&
            storageUri.Scheme == Uri.UriSchemeHttps;
        var documentIntelligenceConfigured =
            Uri.TryCreate(documentIntelligence.Endpoint, UriKind.Absolute, out var documentUri) &&
            documentUri.Scheme == Uri.UriSchemeHttps;

        return Task.FromResult(new IntegrationHealthResponse(
            storageConfigured,
            documentIntelligenceConfigured,
            new AgentsHealthResponse(
                new AgentServiceHealth("NotChecked"),
                new AgentServiceHealth("NotChecked"),
                null,
                timeProvider.GetUtcNow())));
    }

    private Task<Invoice?> FindInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        dbContext.Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invoice =>
                    invoice.OrganizationId == currentUser.OrganizationId &&
                    invoice.Id == invoiceId,
                cancellationToken);

    private static AgentInvoiceData MapInvoice(Invoice invoice)
    {
        var confidence = JsonSerializer.Deserialize<Dictionary<string, double?>>(
            invoice.FieldConfidenceJson,
            JsonOptions) ?? [];
        var warnings = JsonSerializer.Deserialize<List<string>>(
            invoice.ValidationWarningsJson,
            JsonOptions) ?? [];

        return new AgentInvoiceData(
            invoice.Id,
            Limit(invoice.SupplierName, 200),
            null,
            Limit(invoice.InvoiceNumber, 100),
            invoice.IssueDate,
            Limit(invoice.CurrencyCode, 3),
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.Total,
            invoice.Status.ToString(),
            confidence
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Take(60)
                .ToDictionary(
                    item => Limit(item.Key, 100) ?? string.Empty,
                    item => item.Value,
                    StringComparer.Ordinal),
            warnings
                .Take(30)
                .Select(warning => Limit(warning, 250) ?? string.Empty)
                .ToList());
    }

    private static InvoiceDuplicateMatch MapDuplicateMatch(Invoice source, Invoice candidate)
    {
        var sameFile = !string.IsNullOrWhiteSpace(source.Sha256) &&
                       string.Equals(source.Sha256, candidate.Sha256, StringComparison.Ordinal);
        var sameBusinessIdentity =
            !string.IsNullOrWhiteSpace(source.SupplierTaxIdNormalized) &&
            !string.IsNullOrWhiteSpace(source.InvoiceNumberNormalized) &&
            source.IssueDate.HasValue &&
            source.Total.HasValue &&
            source.SupplierTaxIdNormalized == candidate.SupplierTaxIdNormalized &&
            source.InvoiceNumberNormalized == candidate.InvoiceNumberNormalized &&
            source.IssueDate == candidate.IssueDate &&
            source.Total == candidate.Total;
        var matchedRules = sameFile
            ? new[] { "sha256" }
            : sameBusinessIdentity
                ? new[] { "supplierTaxId", "invoiceNumber", "issueDate", "total" }
                : ["existingDuplicateLink"];
        var matchType = sameFile
            ? "ExactFileDuplicate"
            : sameBusinessIdentity
                ? "ExactBusinessDuplicate"
                : "ExistingDuplicateLink";

        return new InvoiceDuplicateMatch(
            candidate.Id,
            Limit(candidate.InvoiceNumber, 100),
            candidate.IssueDate,
            candidate.Total,
            Limit(candidate.CurrencyCode, 3),
            matchType,
            matchedRules,
            null);
    }

    private static string? Limit(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
