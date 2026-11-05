using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Domain;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Persistence;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class InventoryEndpointTests
{
    [Fact]
    public async Task InventoryListReturnsOnlyCurrentOrganizationWithStockAndPagination()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var productId = Guid.NewGuid();
        var otherOrganizationProductId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            dbContext.Products.Add(new Product(
                productId,
                DemoIdentity.OrganizationId,
                "STOCK-001",
                "Producto con stock",
                "unidad"));
            dbContext.InventoryItems.Add(new InventoryItem(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                productId,
                2m,
                5m));

            var otherOrganizationId = Guid.NewGuid();
            dbContext.Organizations.Add(new Organization(otherOrganizationId, "Otra organización"));
            dbContext.Products.Add(new Product(
                otherOrganizationProductId,
                otherOrganizationId,
                "PRIVATE-001",
                "Producto de otra organización",
                "unidad"));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/api/inventario?page=1&pageSize=1&buscar=stock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(productId, item.GetProperty("productId").GetGuid());
        Assert.Equal("STOCK-001", item.GetProperty("sku").GetString());
        Assert.Equal(2m, item.GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(5m, item.GetProperty("reorderThreshold").GetDecimal());
        Assert.True(item.GetProperty("isBelowReorderThreshold").GetBoolean());

        using var ownOrganizationDetail = await client.GetAsync($"/api/inventario/{productId}");
        Assert.Equal(HttpStatusCode.OK, ownOrganizationDetail.StatusCode);

        using var otherOrganizationItem = await client.GetAsync(
            "/api/inventario?buscar=otra%20organizaci%C3%B3n");
        Assert.Equal(HttpStatusCode.OK, otherOrganizationItem.StatusCode);
        using var otherOrganizationJson =
            JsonDocument.Parse(await otherOrganizationItem.Content.ReadAsStringAsync());
        Assert.Equal(0, otherOrganizationJson.RootElement.GetProperty("totalItems").GetInt32());

        using var crossOrganizationDetail = await client.GetAsync(
            $"/api/inventario/{otherOrganizationProductId}");
        Assert.Equal(HttpStatusCode.NotFound, crossOrganizationDetail.StatusCode);
    }

    [Theory]
    [InlineData("/api/inventario?page=0")]
    [InlineData("/api/inventario?pageSize=101")]
    [InlineData("/api/inventario?page=2147483647&pageSize=100")]
    [InlineData("/api/inventario?buscar=12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    public async Task InventoryListRejectsInvalidQueryParameters(string path)
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "invalid_inventory_query",
            json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
}
