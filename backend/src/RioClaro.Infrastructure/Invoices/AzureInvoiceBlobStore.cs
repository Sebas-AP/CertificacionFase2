extern alias AzureIdentity;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RioClaro.Application.Invoices;

namespace RioClaro.Infrastructure.Invoices;

public sealed class AzureInvoiceBlobStore(
    IOptions<AzureStorageOptions> storageOptions,
    ILogger<AzureInvoiceBlobStore> logger) : IInvoiceBlobStore
{
    public async Task<InvoiceBlobReceipt> UploadAsync(
        string blobName,
        Stream content,
        InvoiceFileMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var container = CreateContainerClient();
            await container.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.None,
                cancellationToken: cancellationToken);
            var properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.PublicAccess is not null)
            {
                throw new InvoiceDependencyException("blob_storage", "blob_container_must_be_private");
            }

            var blob = container.GetBlobClient(blobName);
            var upload = await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = metadata.ContentType },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["originalfilenamebase64"] = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes(metadata.OriginalFileName)),
                        ["sha256"] = metadata.Sha256,
                        ["sizebytes"] = metadata.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["pagecount"] = metadata.PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["uploadedbyuserid"] = metadata.UploadedByUserId.ToString("D"),
                        ["uploadedatutc"] = metadata.UploadedAtUtc.ToString("O"),
                        ["retainuntilutc"] = metadata.RetainUntilUtc.ToString("O")
                    }
                },
                cancellationToken);

            return new InvoiceBlobReceipt(upload.Value.ETag.ToString());
        }
        catch (InvoiceDependencyException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Blob upload failed. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw new InvoiceDependencyException("blob_storage", "blob_storage_failed");
        }
    }

    public async Task<Stream> OpenReadAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await CreateContainerClient()
                .GetBlobClient(blobName)
                .OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (InvoiceDependencyException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Blob download failed. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw new InvoiceDependencyException("blob_storage", "invoice_pdf_unavailable");
        }
    }

    public async Task<bool> ExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CreateContainerClient()
                .GetBlobClient(blobName)
                .ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (InvoiceDependencyException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Blob existence check failed. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw new InvoiceDependencyException("blob_storage", "invoice_pdf_unavailable");
        }
    }

    public async Task DeleteIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateContainerClient()
                .GetBlobClient(blobName)
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (InvoiceDependencyException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Blob compensation failed. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw new InvoiceDependencyException("blob_storage", "blob_compensation_failed");
        }
    }

    private BlobContainerClient CreateContainerClient()
    {
        var options = storageOptions.Value;
        if (Uri.TryCreate(options.ServiceUri, UriKind.Absolute, out var serviceUri))
        {
            return new BlobContainerClient(
                new Uri($"{serviceUri.AbsoluteUri.TrimEnd('/')}/{options.InvoiceContainerName}"),
                new AzureIdentity::Azure.Identity.DefaultAzureCredential());
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new BlobContainerClient(options.ConnectionString, options.InvoiceContainerName);
        }

        throw new InvoiceDependencyException("blob_storage", "blob_storage_not_configured");
    }
}
