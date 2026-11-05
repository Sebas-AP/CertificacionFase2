using Microsoft.EntityFrameworkCore;
using RioClaro.Application.Inventory;

namespace RioClaro.Infrastructure.Persistence;

public sealed class InventoryQueryService(RioClaroDbContext dbContext) : IInventoryQueryService
{
    public async Task<InventoryPageResponse> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var products = dbContext.Products
            .AsNoTracking()
            .Where(product => product.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            products = products.Where(product =>
                product.Name.Contains(searchTerm) || product.Sku.Contains(searchTerm));
        }

        var totalItems = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new InventoryItemResponse(
                product.Id,
                product.Sku,
                product.Name,
                product.Unit,
                product.Inventory == null ? null : product.Inventory.QuantityOnHand,
                product.Inventory == null ? null : product.Inventory.ReorderThreshold,
                product.Inventory == null
                    ? null
                    : product.Inventory.QuantityOnHand <= product.Inventory.ReorderThreshold))
            .ToListAsync(cancellationToken);

        return new InventoryPageResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public Task<InventoryItemResponse?> GetByIdAsync(
        Guid organizationId,
        Guid productId,
        CancellationToken cancellationToken = default) =>
        dbContext.Products
            .AsNoTracking()
            .Where(product => product.OrganizationId == organizationId && product.Id == productId)
            .Select(product => new InventoryItemResponse(
                product.Id,
                product.Sku,
                product.Name,
                product.Unit,
                product.Inventory == null ? null : product.Inventory.QuantityOnHand,
                product.Inventory == null ? null : product.Inventory.ReorderThreshold,
                product.Inventory == null
                    ? null
                    : product.Inventory.QuantityOnHand <= product.Inventory.ReorderThreshold))
            .SingleOrDefaultAsync(cancellationToken);
}
