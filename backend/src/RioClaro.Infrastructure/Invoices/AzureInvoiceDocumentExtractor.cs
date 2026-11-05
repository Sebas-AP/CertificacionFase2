extern alias AzureIdentity;

using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RioClaro.Application.Invoices;
using RioClaro.Domain;
using System.ClientModel.Primitives;

namespace RioClaro.Infrastructure.Invoices;

public sealed class AzureInvoiceDocumentExtractor(
    IOptions<AzureDocumentIntelligenceOptions> documentOptions,
    ILogger<AzureInvoiceDocumentExtractor> logger) : IInvoiceDocumentExtractor
{
    public async Task<InvoiceExtractionData> ExtractAsync(
        Stream pdf,
        CancellationToken cancellationToken = default)
    {
        var settings = documentOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            throw new InvoiceDependencyException(
                "document_intelligence",
                "document_intelligence_not_configured");
        }

        try
        {
            var client = CreateClient(settings);
            var context = new RequestContext { CancellationToken = cancellationToken };
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                settings.ModelId,
                RequestContent.Create(pdf),
                context: context);
            var result = ModelReaderWriter.Read<AnalyzeResult>(operation.Value)
                ?? throw new InvoiceDependencyException(
                    "document_intelligence",
                    "document_intelligence_invalid_response");
            return MapResult(result);
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                "Document Intelligence request failed. ErrorType={ErrorType}; Status={Status}",
                exception.GetType().Name,
                exception.Status);
            throw new InvoiceDependencyException(
                "document_intelligence",
                "document_intelligence_failed");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Document Intelligence processing failed. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw new InvoiceDependencyException(
                "document_intelligence",
                "document_intelligence_failed");
        }
    }

    private static DocumentIntelligenceClient CreateClient(AzureDocumentIntelligenceOptions options)
    {
        var endpoint = new Uri(options.Endpoint);
        return string.IsNullOrWhiteSpace(options.ApiKey)
            ? new DocumentIntelligenceClient(
                endpoint,
                new AzureIdentity::Azure.Identity.DefaultAzureCredential())
            : new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(options.ApiKey));
    }

    private static InvoiceExtractionData MapResult(AnalyzeResult result)
    {
        var document = result.Documents.FirstOrDefault();
        if (document is null)
        {
            return new InvoiceExtractionData(
                null, null, null, null, null, null, null, null,
                null, null, null, null, [], new Dictionary<string, double?>());
        }

        var confidence = new Dictionary<string, double?>(StringComparer.Ordinal);
        var fields = document.Fields;

        string? ReadString(string fieldName)
        {
            if (!fields.TryGetValue(fieldName, out var field))
            {
                confidence[fieldName] = null;
                return null;
            }

            confidence[fieldName] = field.Confidence;
            return field.FieldType == DocumentFieldType.String ? field.ValueString : null;
        }

        DateOnly? ReadDate(string fieldName)
        {
            if (!fields.TryGetValue(fieldName, out var field))
            {
                confidence[fieldName] = null;
                return null;
            }

            confidence[fieldName] = field.Confidence;
            return field.FieldType == DocumentFieldType.Date
                ? DateOnly.FromDateTime(field.ValueDate!.Value.UtcDateTime)
                : null;
        }

        decimal? ReadCurrency(string fieldName)
        {
            if (!fields.TryGetValue(fieldName, out var field))
            {
                confidence[fieldName] = null;
                return null;
            }

            confidence[fieldName] = field.Confidence;
            return field.FieldType == DocumentFieldType.Currency
                ? Convert.ToDecimal(field.ValueCurrency.Amount, System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        var items = new List<InvoiceExtractionLine>();
        if (fields.TryGetValue("Items", out var itemsField) &&
            itemsField.FieldType == DocumentFieldType.List)
        {
            for (var itemIndex = 0; itemIndex < itemsField.ValueList.Count; itemIndex++)
            {
                var item = itemsField.ValueList[itemIndex];
                if (item.FieldType != DocumentFieldType.Dictionary)
                {
                    continue;
                }

                var itemFields = item.ValueDictionary;
                string? description = null;
                decimal? quantity = null;
                decimal? unitPrice = null;
                decimal? amount = null;

                if (itemFields.TryGetValue("Description", out var descriptionField) &&
                    descriptionField.FieldType == DocumentFieldType.String)
                {
                    description = descriptionField.ValueString;
                    confidence[$"Items[{itemIndex}].Description"] = descriptionField.Confidence;
                }

                if (itemFields.TryGetValue("Quantity", out var quantityField) &&
                    (quantityField.FieldType == DocumentFieldType.Double ||
                     quantityField.FieldType == DocumentFieldType.Int64))
                {
                    quantity = quantityField.FieldType == DocumentFieldType.Int64
                        ? quantityField.ValueInt64
                        : Convert.ToDecimal(
                            quantityField.ValueDouble,
                            System.Globalization.CultureInfo.InvariantCulture);
                    confidence[$"Items[{itemIndex}].Quantity"] = quantityField.Confidence;
                }

                if (itemFields.TryGetValue("UnitPrice", out var priceField) &&
                    priceField.FieldType == DocumentFieldType.Currency)
                {
                    unitPrice = Convert.ToDecimal(priceField.ValueCurrency.Amount, System.Globalization.CultureInfo.InvariantCulture);
                    confidence[$"Items[{itemIndex}].UnitPrice"] = priceField.Confidence;
                }

                if (itemFields.TryGetValue("Amount", out var amountField) &&
                    amountField.FieldType == DocumentFieldType.Currency)
                {
                    amount = Convert.ToDecimal(amountField.ValueCurrency.Amount, System.Globalization.CultureInfo.InvariantCulture);
                    confidence[$"Items[{itemIndex}].Amount"] = amountField.Confidence;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    items.Add(new InvoiceExtractionLine(
                        description,
                        quantity,
                        unitPrice,
                        amount,
                        item.Confidence));
                }
            }
        }

        var invoiceNumber = ReadString("InvoiceId");
        var invoiceDate = ReadDate("InvoiceDate");
        var dueDate = ReadDate("DueDate");
        var subtotal = ReadCurrency("SubTotal");
        var tax = ReadCurrency("TotalTax");
        var total = ReadCurrency("InvoiceTotal");
        var supplierName = ReadString("VendorName");
        var supplierTaxId = ReadString("VendorTaxId");
        var customerName = ReadString("CustomerName");
        var customerTaxId = ReadString("CustomerTaxId");
        var purchaseOrder = ReadString("PurchaseOrder");
        var currency = fields.TryGetValue("InvoiceTotal", out var totalField) &&
                       totalField.FieldType == DocumentFieldType.Currency
            ? totalField.ValueCurrency.CurrencyCode
            : null;
        if (totalField is not null)
        {
            confidence["Currency"] = totalField.Confidence;
        }

        return new InvoiceExtractionData(
            supplierName,
            supplierTaxId,
            customerName,
            customerTaxId,
            invoiceNumber,
            invoiceDate,
            dueDate,
            purchaseOrder,
            subtotal,
            tax,
            total,
            currency,
            items,
            confidence);
    }
}
