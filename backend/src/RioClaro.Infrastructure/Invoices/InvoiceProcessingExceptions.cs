namespace RioClaro.Infrastructure.Invoices;

public sealed class InvoiceInputException(
    string code,
    string title,
    string detail,
    int statusCode) : Exception(detail)
{
    public string Code { get; } = code;
    public string Title { get; } = title;
    public int StatusCode { get; } = statusCode;
}

public sealed class InvoiceDependencyException(
    string dependency,
    string code) : Exception(code)
{
    public string Dependency { get; } = dependency;
    public string Code { get; } = code;
}
