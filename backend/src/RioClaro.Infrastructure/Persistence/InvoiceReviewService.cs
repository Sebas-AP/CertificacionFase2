using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RioClaro.Application.Invoices;
using RioClaro.Domain.Entities;

namespace RioClaro.Infrastructure.Persistence;

public sealed class InvoiceReviewService(RioClaroDbContext dbContext) : IInvoiceReviewService
{
    public async Task<InvoicePageResponse> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsedStatus = status.Trim().ToLowerInvariant() switch
            {
                "pending_review" => InvoiceStatus.PendingReview,
                "approved" => InvoiceStatus.Approved,
                "extraction_failed" => InvoiceStatus.ExtractionFailed,
                "extracted" => InvoiceStatus.Extracted,
                "needs_validation" => InvoiceStatus.NeedsValidation,
                "duplicate" => InvoiceStatus.Duplicate,
                _ => throw new ArgumentException("Unsupported invoice status.", nameof(status))
            };
            query = query.Where(invoice => invoice.Status == parsedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var entities = await query
            .Include(invoice => invoice.Lines)
            .OrderByDescending(invoice => invoice.UploadedAtUtc)
            .ThenBy(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var invoices = entities
            .Select(invoice => InvoiceResponseMapper.Map(invoice, includeReviewHistory: false))
            .ToList();

        return new InvoicePageResponse(
            invoices,
            page,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<InvoiceResponse?> GetByIdAsync(
        Guid organizationId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.ReviewHistory)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == invoiceId,
                cancellationToken);
        return invoice is null ? null : InvoiceResponseMapper.Map(invoice);
    }

    public async Task<InvoiceActionResult> ReviewAsync(
        Guid organizationId,
        Guid userId,
        Guid invoiceId,
        ReviewInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == invoiceId,
                cancellationToken);
        if (invoice is null)
        {
            return new InvoiceActionResult(null, NotFound: true, Conflict: null);
        }

        var changedFields = new List<string>();
        if (invoice.InvoiceNumber != request.InvoiceNumber) changedFields.Add("invoiceNumber");
        if (invoice.IssueDate != request.IssueDate) changedFields.Add("issueDate");
        if (invoice.CurrencyCode != request.CurrencyCode) changedFields.Add("currencyCode");
        if (invoice.Subtotal != request.Subtotal) changedFields.Add("subtotal");
        if (invoice.TaxAmount != request.TaxAmount) changedFields.Add("taxAmount");
        if (invoice.Total != request.Total) changedFields.Add("total");

        if (invoice.Status is not (InvoiceStatus.PendingReview or
            InvoiceStatus.Extracted or InvoiceStatus.NeedsValidation))
        {
            return new InvoiceActionResult(
                null,
                NotFound: false,
                "Only invoices pending review can be edited.");
        }

        invoice.ApplyReview(
            request.InvoiceNumber,
            request.IssueDate,
            request.CurrencyCode,
            request.Subtotal,
            request.TaxAmount,
            request.Total);

        dbContext.InvoiceReviewHistory.Add(new InvoiceReviewHistory(
            Guid.NewGuid(),
            organizationId,
            invoiceId,
            userId,
            "Reviewed",
            JsonSerializer.Serialize(new
            {
                changedFields,
                reason = request.Reason
            }),
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await GetByIdAsync(organizationId, invoiceId, cancellationToken);
        return new InvoiceActionResult(response, NotFound: false, Conflict: null);
    }

    public async Task<InvoiceActionResult> ApproveAsync(
        Guid organizationId,
        Guid userId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == invoiceId,
                cancellationToken);
        if (invoice is null)
        {
            return new InvoiceActionResult(null, NotFound: true, Conflict: null);
        }

        if (invoice.Status != InvoiceStatus.PendingReview)
        {
            return new InvoiceActionResult(
                null,
                NotFound: false,
                "Only invoices pending review can be approved.");
        }

        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ||
            invoice.IssueDate is null ||
            string.IsNullOrWhiteSpace(invoice.CurrencyCode) ||
            invoice.Total is null)
        {
            return new InvoiceActionResult(
                null,
                NotFound: false,
                "Invoice number, issue date, currency, and total are required for approval.");
        }

        invoice.Approve();
        dbContext.InvoiceReviewHistory.Add(new InvoiceReviewHistory(
            Guid.NewGuid(),
            organizationId,
            invoiceId,
            userId,
            "Approved",
            "{}",
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await GetByIdAsync(organizationId, invoiceId, cancellationToken);
        return new InvoiceActionResult(response, NotFound: false, Conflict: null);
    }
}
