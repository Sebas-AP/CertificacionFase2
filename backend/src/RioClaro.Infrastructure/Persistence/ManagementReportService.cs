using Microsoft.EntityFrameworkCore;
using RioClaro.Application.Reports;
using RioClaro.Domain.Entities;

namespace RioClaro.Infrastructure.Persistence;

public sealed class ManagementReportService(RioClaroDbContext dbContext) : IManagementReportService
{
    public async Task<ManagementReportResponse> GetAsync(
        Guid organizationId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var invoices = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.OrganizationId == organizationId &&
                invoice.UploadedAtUtc >= fromUtc &&
                invoice.UploadedAtUtc < toUtc);

        var processedCount = await invoices
            .CountAsync(
                invoice => invoice.ExtractionStatus == InvoiceExtractionStatus.Succeeded,
                cancellationToken);
        var duplicateCount = await invoices
            .CountAsync(invoice => invoice.Status == InvoiceStatus.Duplicate, cancellationToken);
        var invoiceCount = await invoices.CountAsync(cancellationToken);
        var duplicateRate = invoiceCount == 0 ? 0m : (decimal)duplicateCount / invoiceCount;

        var approvedAmounts = await invoices
            .Where(invoice =>
                invoice.Status == InvoiceStatus.Approved &&
                invoice.Total != null &&
                invoice.CurrencyCode != null)
            .Select(invoice => new
            {
                invoice.CurrencyCode,
                Total = invoice.Total!.Value
            })
            .ToListAsync(cancellationToken);
        var approvedTotals = approvedAmounts
            .GroupBy(invoice => invoice.CurrencyCode!, StringComparer.Ordinal)
            .Select(group => new CurrencyInvoiceTotal(
                group.Key,
                group.Sum(invoice => invoice.Total)))
            .OrderBy(total => total.CurrencyCode, StringComparer.Ordinal)
            .ToList();

        var criticalInventoryCount = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == organizationId &&
                item.QuantityOnHand <= item.ReorderThreshold)
            .CountAsync(cancellationToken);

        return new ManagementReportResponse(
            fromUtc,
            toUtc,
            processedCount,
            duplicateCount,
            duplicateRate,
            approvedTotals,
            criticalInventoryCount);
    }
}
