using Microsoft.Extensions.Options;

namespace RioClaro.Infrastructure.Invoices;

public sealed class PdfUploadOptions
{
    public const string SectionName = "PdfUpload";

    public long MaxFileSizeBytes { get; set; } = 20_971_520;
    public int MaxPages { get; set; } = 50;
    public int RetentionDays { get; set; } = 60;
    public string[] AllowedContentTypes { get; set; } = ["application/pdf"];
    public int ProcessingTimeoutSeconds { get; set; } = 120;
    public int MaxExtractionRetries { get; set; } = 3;

    public bool IsValid(out string? failure)
    {
        if (MaxFileSizeBytes != 20_971_520)
        {
            failure = "PdfUpload:MaxFileSizeBytes debe ser exactamente 20971520 bytes (20 MiB).";
        }
        else if (MaxPages != 50)
        {
            failure = "PdfUpload:MaxPages debe ser exactamente 50.";
        }
        else if (RetentionDays != 60)
        {
            failure = "PdfUpload:RetentionDays debe ser exactamente 60.";
        }
        else if (AllowedContentTypes is not { Length: 1 } allowedContentTypes ||
                 !string.Equals(allowedContentTypes[0], "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            failure = "PdfUpload:AllowedContentTypes solo puede contener application/pdf.";
        }
        else if (ProcessingTimeoutSeconds is < 1 or > 600)
        {
            failure = "PdfUpload:ProcessingTimeoutSeconds debe estar entre 1 y 600.";
        }
        else if (MaxExtractionRetries is < 1 or > 3)
        {
            failure = "PdfUpload:MaxExtractionRetries debe estar entre 1 y 3.";
        }
        else
        {
            failure = null;
        }

        return failure is null;
    }
}

public sealed class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ServiceUri { get; set; } = string.Empty;
    public string InvoiceContainerName { get; set; } = "invoice-originals";
}

public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-invoice";
}

public sealed class InvoiceServiceOptionsValidator : IValidateOptions<PdfUploadOptions>
{
    public ValidateOptionsResult Validate(string? name, PdfUploadOptions options) =>
        options.IsValid(out var failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure!);
}

public sealed class AzureInvoiceOptionsValidator : IValidateOptions<AzureStorageOptions>,
    IValidateOptions<AzureDocumentIntelligenceOptions>
{
    ValidateOptionsResult IValidateOptions<AzureStorageOptions>.Validate(
        string? name,
        AzureStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InvoiceContainerName) ||
            options.InvoiceContainerName.Length > 63 ||
            options.InvoiceContainerName.Any(character =>
                !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-')) ||
            options.InvoiceContainerName.StartsWith('-') ||
            options.InvoiceContainerName.EndsWith('-') ||
            options.InvoiceContainerName.Contains("--", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "AzureStorage:InvoiceContainerName debe ser un nombre válido de contenedor Blob.");
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceUri) &&
            (!Uri.TryCreate(options.ServiceUri, UriKind.Absolute, out var serviceUri) ||
             serviceUri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("AzureStorage:ServiceUri debe ser una URL HTTPS absoluta.");
        }

        return ValidateOptionsResult.Success;
    }

    ValidateOptionsResult IValidateOptions<AzureDocumentIntelligenceOptions>.Validate(
        string? name,
        AzureDocumentIntelligenceOptions options)
    {
        if (!string.Equals(options.ModelId, "prebuilt-invoice", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "AzureDocumentIntelligence:ModelId debe ser prebuilt-invoice.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint) &&
            (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail(
                "AzureDocumentIntelligence:Endpoint debe ser una URL HTTPS absoluta.");
        }

        return ValidateOptionsResult.Success;
    }
}
