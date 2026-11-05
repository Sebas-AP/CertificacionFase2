namespace RioClaro.Application.Inventory;

public sealed record InventoryItemResponse(
    Guid ProductId,
    string Sku,
    string Name,
    string Unit,
    decimal? QuantityOnHand,
    decimal? ReorderThreshold,
    bool? IsBelowReorderThreshold);

public sealed record InventoryPageResponse(
    IReadOnlyList<InventoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public interface IInventoryQueryService
{
    Task<InventoryPageResponse> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<InventoryItemResponse?> GetByIdAsync(
        Guid organizationId,
        Guid productId,
        CancellationToken cancellationToken = default);
}
