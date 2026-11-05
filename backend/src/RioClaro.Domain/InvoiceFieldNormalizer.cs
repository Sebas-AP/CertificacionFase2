using System.Text;

namespace RioClaro.Domain;

public static class InvoiceFieldNormalizer
{
    private static readonly string[] CommonInvoicePrefixes =
        ["FACTURA", "INVOICE", "FOLIO", "FACT", "INV"];

    public static string? NormalizeTaxId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value
            .ToUpperInvariant()
            .Where(char.IsAsciiLetterOrDigit)
            .ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    public static string? NormalizeInvoiceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsAsciiLetterOrDigit)
            .ToArray());

        foreach (var prefix in CommonInvoicePrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal) &&
                normalized.Length > prefix.Length &&
                char.IsAsciiDigit(normalized[prefix.Length]))
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
