using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RioClaro.Application.Invoices;
using RioClaro.Infrastructure.Persistence;

namespace RioClaro.Infrastructure.Invoices;

public sealed class InvoiceRetentionService(
    RioClaroDbContext dbContext,
    IInvoiceBlobStore blobStore,
    TimeProvider timeProvider,
    ILogger<InvoiceRetentionService> logger) : IInvoiceRetentionService
{
    public async Task<InvoiceRetentionRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                !invoice.LegalHold &&
                invoice.PdfDeletedAtUtc == null &&
                invoice.RetainUntilUtc <= now)
            .OrderBy(invoice => invoice.RetainUntilUtc)
            .Select(invoice => invoice.Id)
            .ToListAsync(cancellationToken);

        var deletedCount = 0;
        var failedCount = 0;
        foreach (var invoiceId in candidates)
        {
            var invoice = await dbContext.Invoices
                .SingleOrDefaultAsync(
                    item => item.Id == invoiceId &&
                            !item.LegalHold &&
                            item.PdfDeletedAtUtc == null &&
                            item.RetainUntilUtc <= now,
                    cancellationToken);
            if (invoice is null)
            {
                continue;
            }

            try
            {
                await blobStore.DeleteIfExistsAsync(invoice.DocumentStorageKey, cancellationToken);
                invoice.MarkPdfDeleted(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                deletedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                dbContext.ChangeTracker.Clear();
                failedCount++;
                logger.LogError(
                    "Invoice PDF retention failed. InvoiceId={InvoiceId}; ErrorType={ErrorType}",
                    invoiceId,
                    exception.GetType().Name);
            }
        }

        return new InvoiceRetentionRunResult(deletedCount, failedCount, now);
    }
}

public sealed class InvoiceRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<InvoiceRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(RunInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceSafelyAsync(stoppingToken);
        }
    }

    private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<IInvoiceRetentionService>();
            var result = await retentionService.RunOnceAsync(cancellationToken);
            if (result.FailedPdfCount > 0)
            {
                logger.LogWarning(
                    "Invoice PDF retention completed with failures. DeletedCount={DeletedCount}; FailedCount={FailedCount}",
                    result.DeletedPdfCount,
                    result.FailedPdfCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Invoice PDF retention run failed. ErrorType={ErrorType}",
                exception.GetType().Name);
        }
    }
}
