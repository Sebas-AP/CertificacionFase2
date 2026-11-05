using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RioClaro.Application;
using RioClaro.Application.Invoices;
using RioClaro.Domain;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Persistence;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace RioClaro.Infrastructure.Invoices;

public sealed class InvoiceUploadService(
    RioClaroDbContext dbContext,
    IInvoiceBlobStore blobStore,
    IInvoiceDocumentExtractor documentExtractor,
    ICurrentUserContext currentUser,
    IOptions<PdfUploadOptions> uploadOptions,
    TimeProvider timeProvider,
    ILogger<InvoiceUploadService> logger) : IInvoiceUploadService
{
    private const int BufferSize = 64 * 1024;
    private const double MinimumImportantFieldConfidence = 0.80;
    private const decimal MaximumMoney = 999_999_999_999_999.9999m;
    private const decimal MaximumQuantity = 99_999_999_999_999.9999m;

    public async Task<InvoiceUploadOperationResult> UploadAsync(
        Stream pdf,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadMetadata(originalFileName, contentType, sizeBytes);
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"rioclaro-{Guid.NewGuid():N}.pdf");

        try
        {
            var (actualSize, sha256, prefix) = await CopyAndHashAsync(
                pdf,
                temporaryPath,
                cancellationToken);
            if (actualSize != sizeBytes)
            {
                throw InvalidPdf("pdf_size_mismatch", "El tamaño recibido no coincide con el archivo enviado.");
            }

            if (!prefix.AsSpan().StartsWith("%PDF-"u8))
            {
                throw InvalidPdf("invalid_pdf_signature", "El contenido no tiene una firma PDF válida.");
            }

            var pageCount = InspectPdf(temporaryPath, correlationId);
            if (pageCount > uploadOptions.Value.MaxPages)
            {
                throw new InvoiceInputException(
                    "pdf_page_limit_exceeded",
                    "El PDF excede el límite de páginas.",
                    $"El máximo permitido es {uploadOptions.Value.MaxPages} páginas.",
                    (int)HttpStatusCode.BadRequest);
            }

            var existing = await dbContext.Invoices
                .AsNoTracking()
                .Where(invoice =>
                    invoice.OrganizationId == currentUser.OrganizationId &&
                    invoice.Sha256 == sha256)
                .Select(invoice => (Guid?)invoice.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return new InvoiceUploadOperationResult(
                    null,
                    Duplicate: true,
                    DuplicateInvoiceId: existing,
                    Conflict: "El archivo ya fue cargado para esta organización.");
            }

            var now = timeProvider.GetUtcNow();
            var invoiceId = Guid.NewGuid();
            var blobName = BuildBlobName(currentUser.OrganizationId, invoiceId, now);
            var safeFileName = NormalizeFileName(originalFileName);
            var metadata = new InvoiceFileMetadata(
                safeFileName,
                "application/pdf",
                actualSize,
                sha256,
                pageCount,
                currentUser.UserId,
                now,
                now.AddDays(uploadOptions.Value.RetentionDays));

            InvoiceBlobReceipt blobReceipt;
            try
            {
                await using var storageStream = new FileStream(
                    temporaryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                blobReceipt = await blobStore.UploadAsync(
                    blobName,
                    storageStream,
                    metadata,
                    cancellationToken);
            }
            catch (InvoiceDependencyException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    "Invoice blob upload failed. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                    correlationId,
                    exception.GetType().Name);
                throw new InvoiceDependencyException("blob_storage", "blob_storage_failed");
            }

            var invoice = new Invoice(
                invoiceId,
                currentUser.OrganizationId,
                currentUser.UserId,
                blobName,
                now);
            invoice.SetUploadMetadata(
                safeFileName,
                metadata.ContentType,
                actualSize,
                sha256,
                pageCount,
                blobReceipt.ETag,
                metadata.RetainUntilUtc);
            if (!invoice.BeginExtractionAttempt(
                    uploadOptions.Value.MaxExtractionRetries,
                    now,
                    TimeSpan.FromSeconds(uploadOptions.Value.ProcessingTimeoutSeconds)))
            {
                await CompensateBlobAsync(blobName, correlationId);
                throw new InvalidOperationException("No fue posible iniciar la extracción de la factura.");
            }

            var extraction = await ExtractOrCaptureFailureAsync(
                temporaryPath,
                correlationId,
                cancellationToken);
            var completedAt = timeProvider.GetUtcNow();
            if (extraction.Data is null)
            {
                invoice.MarkExtractionFailed(extraction.ErrorCode!, completedAt);
            }
            else
            {
                var sanitized = SanitizeExtraction(extraction.Data);
                var warnings = GetValidationWarnings(sanitized.Data)
                    .Concat(sanitized.Warnings)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var duplicateId = await FindBusinessDuplicateAsync(
                    currentUser.OrganizationId,
                    sanitized.Data,
                    cancellationToken);
                invoice.ApplyExtraction(sanitized.Data, warnings, duplicateId, completedAt);
            }

            dbContext.Invoices.Add(invoice);
            try
            {
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateException exception)
            {
                dbContext.ChangeTracker.Clear();
                await CompensateBlobAsync(blobName, correlationId);
                var racedDuplicate = await dbContext.Invoices
                    .AsNoTracking()
                    .Where(item =>
                        item.OrganizationId == currentUser.OrganizationId &&
                        item.Sha256 == sha256)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                if (racedDuplicate is not null)
                {
                    return new InvoiceUploadOperationResult(
                        null,
                        Duplicate: true,
                        DuplicateInvoiceId: racedDuplicate,
                        Conflict: "El archivo ya fue cargado para esta organización.");
                }

                logger.LogError(
                    "Invoice persistence failed after blob upload. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                    correlationId,
                    exception.GetType().Name);
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                dbContext.ChangeTracker.Clear();
                await CompensateBlobAsync(blobName, correlationId);
                logger.LogError(
                    "Invoice persistence failed after blob upload. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                    correlationId,
                    exception.GetType().Name);
                throw;
            }

            var response = await MapInvoiceAsync(invoiceId, cancellationToken);
            return new InvoiceUploadOperationResult(
                new InvoiceUploadResult(
                    response!,
                    extraction.Data is not null,
                    extraction.ErrorCode,
                    GetExtractionErrorMessage(extraction.ErrorCode)),
                Duplicate: false,
                DuplicateInvoiceId: null,
                Conflict: null);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException exception)
            {
                logger.LogWarning(
                    "Temporary invoice file cleanup failed. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                    correlationId,
                    exception.GetType().Name);
            }
        }
    }

    public async Task<InvoiceUploadOperationResult> RetryExtractionAsync(
        Guid invoiceId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == currentUser.OrganizationId && item.Id == invoiceId,
                cancellationToken);
        if (invoice is null)
        {
            return new InvoiceUploadOperationResult(null, false, null, null, NotFound: true);
        }

        if (string.IsNullOrWhiteSpace(invoice.DocumentStorageKey) ||
            !await blobStore.ExistsAsync(invoice.DocumentStorageKey, cancellationToken))
        {
            return new InvoiceUploadOperationResult(
                null,
                false,
                null,
                "El PDF original no está disponible para reintentar la extracción.");
        }

        var now = timeProvider.GetUtcNow();
        if (!invoice.BeginExtractionAttempt(
                uploadOptions.Value.MaxExtractionRetries,
                now,
                TimeSpan.FromSeconds(uploadOptions.Value.ProcessingTimeoutSeconds)))
        {
            return new InvoiceUploadOperationResult(
                null,
                false,
                null,
                invoice.ExtractionAttemptCount >= uploadOptions.Value.MaxExtractionRetries
                    ? "La factura alcanzó el máximo de intentos de extracción."
                    : "Ya existe una extracción en curso para esta factura.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new InvoiceUploadOperationResult(
                null,
                false,
                null,
                "La factura fue modificada por otra solicitud; vuelva a consultar su estado.");
        }
        var extractionResult = await ExtractFromBlobOrCaptureFailureAsync(
            invoice.DocumentStorageKey,
            correlationId,
            cancellationToken);
        var completedAt = timeProvider.GetUtcNow();
        if (extractionResult.Data is null)
        {
            invoice.MarkExtractionFailed(extractionResult.ErrorCode!, completedAt);
        }
        else
        {
            var sanitized = SanitizeExtraction(extractionResult.Data);
            var warnings = GetValidationWarnings(sanitized.Data)
                .Concat(sanitized.Warnings)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var duplicateId = await FindBusinessDuplicateAsync(
                currentUser.OrganizationId,
                sanitized.Data,
                cancellationToken,
                invoice.Id);
            invoice.ApplyExtraction(sanitized.Data, warnings, duplicateId, completedAt);
        }

        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new InvoiceUploadOperationResult(
                null,
                false,
                null,
                "La factura fue modificada por otra solicitud; vuelva a consultar su estado.");
        }

        var response = await MapInvoiceAsync(invoice.Id, cancellationToken);
        return new InvoiceUploadOperationResult(
            new InvoiceUploadResult(
                response!,
                extractionResult.Data is not null,
                extractionResult.ErrorCode,
                GetExtractionErrorMessage(extractionResult.ErrorCode)),
            false,
            null,
            null);
    }

    private async Task<(InvoiceExtractionData? Data, string? ErrorCode)> ExtractOrCaptureFailureAsync(
        string temporaryPath,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return (await ExtractWithTimeoutAsync(stream, cancellationToken), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Invoice extraction timed out. CorrelationId={CorrelationId}",
                correlationId);
            return (null, "document_intelligence_timeout");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Invoice extraction was cancelled. CorrelationId={CorrelationId}",
                correlationId);
            return (null, "document_intelligence_cancelled");
        }
        catch (InvoiceDependencyException exception)
        {
            logger.LogWarning(
                "Invoice extraction dependency failed. CorrelationId={CorrelationId}; Dependency={Dependency}; Code={Code}",
                correlationId,
                exception.Dependency,
                exception.Code);
            return (null, exception.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Invoice extraction failed. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                correlationId,
                exception.GetType().Name);
            return (null, "document_intelligence_failed");
        }
    }

    private async Task<(InvoiceExtractionData? Data, string? ErrorCode)> ExtractFromBlobOrCaptureFailureAsync(
        string blobName,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await blobStore.OpenReadAsync(blobName, cancellationToken);
            return (await ExtractWithTimeoutAsync(stream, cancellationToken), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Invoice extraction timed out. CorrelationId={CorrelationId}",
                correlationId);
            return (null, "document_intelligence_timeout");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Invoice extraction was cancelled. CorrelationId={CorrelationId}",
                correlationId);
            return (null, "document_intelligence_cancelled");
        }
        catch (InvoiceDependencyException exception)
        {
            logger.LogWarning(
                "Invoice extraction dependency failed. CorrelationId={CorrelationId}; Dependency={Dependency}; Code={Code}",
                correlationId,
                exception.Dependency,
                exception.Code);
            return (null, exception.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Invoice extraction failed. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                correlationId,
                exception.GetType().Name);
            return (null, "document_intelligence_failed");
        }
    }

    private async Task<InvoiceExtractionData> ExtractWithTimeoutAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(uploadOptions.Value.ProcessingTimeoutSeconds));
        return await documentExtractor.ExtractAsync(stream, timeout.Token);
    }

    private async Task<Guid?> FindBusinessDuplicateAsync(
        Guid organizationId,
        InvoiceExtractionData extraction,
        CancellationToken cancellationToken,
        Guid? excludedInvoiceId = null)
    {
        var supplierTaxId = InvoiceFieldNormalizer.NormalizeTaxId(extraction.SupplierTaxId);
        var invoiceNumber = InvoiceFieldNormalizer.NormalizeInvoiceNumber(extraction.InvoiceNumber);
        if (supplierTaxId is null ||
            invoiceNumber is null ||
            extraction.InvoiceDate is null ||
            extraction.Total is null)
        {
            return null;
        }

        return await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.OrganizationId == organizationId &&
                invoice.Id != excludedInvoiceId &&
                invoice.SupplierTaxIdNormalized == supplierTaxId &&
                invoice.InvoiceNumberNormalized == invoiceNumber &&
                invoice.IssueDate == extraction.InvoiceDate &&
                invoice.Total == extraction.Total)
            .OrderBy(invoice => invoice.UploadedAtUtc)
            .Select(invoice => (Guid?)invoice.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<string> GetValidationWarnings(InvoiceExtractionData extraction)
    {
        var warnings = new List<string>();
        AddRequiredWarning("InvoiceId", extraction.InvoiceNumber);
        AddRequiredWarning("InvoiceDate", extraction.InvoiceDate);
        AddRequiredWarning("InvoiceTotal", extraction.Total);
        AddRequiredWarning("Currency", extraction.Currency);
        foreach (var field in new[] { "InvoiceId", "InvoiceDate", "InvoiceTotal", "Currency" })
        {
            if (extraction.FieldConfidence.TryGetValue(field, out var confidence) &&
                confidence is not null &&
                confidence < MinimumImportantFieldConfidence)
            {
                warnings.Add($"low_confidence_{ToWarningName(field)}");
            }
        }

        void AddRequiredWarning(string field, object? value)
        {
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            {
                warnings.Add($"missing_{ToWarningName(field)}");
            }
        }

        return warnings;
    }

    private static (InvoiceExtractionData Data, IReadOnlyList<string> Warnings) SanitizeExtraction(
        InvoiceExtractionData extraction)
    {
        var warnings = new List<string>();
        var confidences = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var (field, confidence) in extraction.FieldConfidence)
        {
            confidences[field] = IsValidConfidence(confidence)
                ? confidence
                : null;
            if (confidence is not null && !IsValidConfidence(confidence))
            {
                warnings.Add("invalid_field_confidence");
            }
        }

        var lines = new List<InvoiceExtractionLine>();
        foreach (var line in extraction.LineItems)
        {
            if (string.IsNullOrWhiteSpace(line.Description))
            {
                warnings.Add("invalid_line_item");
                continue;
            }

            var quantity = SanitizeNumber(
                line.Quantity,
                MaximumQuantity,
                "invalid_line_item_quantity",
                warnings);
            var unitPrice = SanitizeNumber(
                line.UnitPrice,
                MaximumMoney,
                "invalid_line_item_unit_price",
                warnings);
            var lineTotal = SanitizeNumber(
                line.LineTotal,
                MaximumMoney,
                "invalid_line_item_total",
                warnings);
            var confidence = IsValidConfidence(line.Confidence) ? line.Confidence : null;
            if (line.Confidence is not null && confidence is null)
            {
                warnings.Add("invalid_line_item_confidence");
            }

            decimal computedTotal;
            try
            {
                computedTotal = lineTotal ?? ((quantity ?? 0m) * (unitPrice ?? 0m));
            }
            catch (OverflowException)
            {
                warnings.Add("invalid_line_item_total");
                continue;
            }

            if (computedTotal > MaximumMoney)
            {
                warnings.Add("invalid_line_item_total");
                continue;
            }

            lines.Add(line with
            {
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal ?? computedTotal,
                Confidence = confidence
            });
        }

        var currency = extraction.Currency?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(currency) &&
            (currency.Length != 3 || !currency.All(char.IsAsciiLetter)))
        {
            currency = null;
            warnings.Add("invalid_currency");
        }

        return (extraction with
        {
            Subtotal = SanitizeNumber(extraction.Subtotal, MaximumMoney, "invalid_subtotal", warnings),
            TaxAmount = SanitizeNumber(extraction.TaxAmount, MaximumMoney, "invalid_tax_amount", warnings),
            Total = SanitizeNumber(extraction.Total, MaximumMoney, "invalid_total", warnings),
            Currency = currency,
            LineItems = lines,
            FieldConfidence = confidences
        }, warnings);
    }

    private static decimal? SanitizeNumber(
        decimal? value,
        decimal maximum,
        string warning,
        ICollection<string> warnings)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0 || value > maximum || decimal.Round(value.Value, 4) != value.Value)
        {
            warnings.Add(warning);
            return null;
        }

        return value;
    }

    private static bool IsValidConfidence(double? confidence) =>
        confidence is null || (double.IsFinite(confidence.Value) && confidence is >= 0 and <= 1);

    private static string? GetExtractionErrorMessage(string? errorCode) => errorCode switch
    {
        null => null,
        "document_intelligence_not_configured" =>
            "Configure Azure Document Intelligence para habilitar la extracción.",
        "document_intelligence_timeout" =>
            "La extracción excedió el tiempo permitido; el PDF se conservó y puede reintentarse.",
        "document_intelligence_cancelled" =>
            "La extracción se canceló; el PDF se conservó y puede reintentarse.",
        _ => "Document Intelligence no pudo extraer los campos; el PDF se conservó y puede reintentarse."
    };

    private static string ToWarningName(string field) => field switch
    {
        "InvoiceId" => "invoice_number",
        "InvoiceDate" => "invoice_date",
        "InvoiceTotal" => "total",
        _ => field.ToLowerInvariant()
    };

    private async Task<InvoiceResponse?> MapInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.ReviewHistory)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == currentUser.OrganizationId &&
                        item.Id == invoiceId,
                cancellationToken);
        return invoice is null ? null : InvoiceResponseMapper.Map(invoice);
    }

    private async Task<(long Size, string Sha256, byte[] Prefix)> CopyAndHashAsync(
        Stream source,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        var prefix = new byte[5];
        var prefixLength = 0;
        long size = 0;

        await using var destination = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            size += read;
            if (size > uploadOptions.Value.MaxFileSizeBytes)
            {
                throw new InvoiceInputException(
                    "pdf_too_large",
                    "El archivo excede el tamaño máximo permitido.",
                    $"El máximo permitido es {uploadOptions.Value.MaxFileSizeBytes} bytes.",
                    (int)HttpStatusCode.RequestEntityTooLarge);
            }

            var toCopy = Math.Min(read, prefix.Length - prefixLength);
            if (toCopy > 0)
            {
                buffer.AsSpan(0, toCopy).CopyTo(prefix.AsSpan(prefixLength));
                prefixLength += toCopy;
            }

            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await destination.FlushAsync(cancellationToken);
        return (size, Convert.ToHexString(hash.GetHashAndReset()), prefix);
    }

    private int InspectPdf(string temporaryPath, string correlationId)
    {
        try
        {
            using var document = PdfDocument.Open(temporaryPath);
            if (document.IsEncrypted)
            {
                throw new InvoiceInputException(
                    "pdf_password_protected",
                    "El PDF está protegido con contraseña.",
                    "Quite la protección con contraseña antes de cargar el documento.",
                    (int)HttpStatusCode.BadRequest);
            }

            if (document.NumberOfPages < 1)
            {
                throw InvalidPdf("invalid_pdf", "El PDF no contiene páginas.");
            }

            return document.NumberOfPages;
        }
        catch (InvoiceInputException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new InvoiceInputException(
                "pdf_password_protected",
                "El PDF está protegido con contraseña.",
                "Quite la protección con contraseña antes de cargar el documento.",
                (int)HttpStatusCode.BadRequest);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "PDF validation failed. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                correlationId,
                exception.GetType().Name);
            throw InvalidPdf("invalid_pdf", "El archivo PDF está corrupto o no se puede leer.");
        }
    }

    private void ValidateUploadMetadata(string fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw InvalidPdf("file_required", "Debe seleccionar un archivo PDF.");
        }

        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidPdf("invalid_file_extension", "El archivo debe tener extensión .pdf.");
        }

        if (!string.Equals(contentType?.Trim(), "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidPdf("invalid_content_type", "El tipo de contenido debe ser application/pdf.");
        }

        if (sizeBytes <= 0)
        {
            throw InvalidPdf("empty_file", "El archivo está vacío.");
        }

        if (sizeBytes > uploadOptions.Value.MaxFileSizeBytes)
        {
            throw new InvoiceInputException(
                "pdf_too_large",
                "El archivo excede el tamaño máximo permitido.",
                $"El máximo permitido es {uploadOptions.Value.MaxFileSizeBytes} bytes.",
                (int)HttpStatusCode.RequestEntityTooLarge);
        }

        if (fileName.Length > 1024)
        {
            throw InvalidPdf("invalid_file_name", "El nombre del archivo no es válido.");
        }
    }

    private static InvoiceInputException InvalidPdf(string code, string detail) =>
        new(code, "Archivo PDF inválido.", detail, (int)HttpStatusCode.BadRequest);

    private static string NormalizeFileName(string fileName)
    {
        var basename = Path.GetFileName(fileName.Replace('\\', '/'));
        var safeName = new string(basename.Where(character =>
            !char.IsControl(character) && character != '/' && character != '\\').ToArray());
        return safeName.Length <= 255 ? safeName : safeName[..255];
    }

    private static string BuildBlobName(Guid organizationId, Guid invoiceId, DateTimeOffset uploadedAtUtc) =>
        $"organizations/{organizationId:D}/invoices/{uploadedAtUtc:yyyy}/{uploadedAtUtc:MM}/{invoiceId:D}/original.pdf";

    private async Task CompensateBlobAsync(string blobName, string correlationId)
    {
        try
        {
            await blobStore.DeleteIfExistsAsync(blobName, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                "Blob compensation failed after persistence error. CorrelationId={CorrelationId}; ErrorType={ErrorType}",
                correlationId,
                exception.GetType().Name);
        }
    }

}
