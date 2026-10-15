using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Domain;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Persistence;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class BusinessDataPersistenceTests
{
    [Fact]
    public async Task BusinessEntitiesPersistWithDecimalAmountsAndOrganizationScopedKeys()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var invoiceId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            var supplier = new Supplier(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                "Proveedor de prueba",
                "FICT-001");
            var invoice = new Invoice(
                invoiceId,
                DemoIdentity.OrganizationId,
                DemoIdentity.UserId,
                "invoices/demo/test.pdf",
                DateTimeOffset.Parse("2026-10-14T12:00:00Z"));
            invoice.Lines.Add(new InvoiceLine(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                invoiceId,
                "Renglón de prueba",
                2.2500m,
                3.1250m,
                7.0313m));
            var product = new Product(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                "TEST-001",
                "Producto de prueba",
                "unidad");
            var inventory = new InventoryItem(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                product.Id,
                12.5000m,
                2.0000m);

            dbContext.Suppliers.Add(supplier);
            dbContext.Invoices.Add(invoice);
            dbContext.Products.Add(product);
            dbContext.InventoryItems.Add(inventory);
            await dbContext.SaveChangesAsync();

            var persistedInvoice = await dbContext.Invoices
                .Include(item => item.Lines)
                .SingleAsync(item => item.Id == invoiceId);
            Assert.Equal(7.0313m, Assert.Single(persistedInvoice.Lines).LineTotal);

            var persistedProduct = await dbContext.Products
                .Include(item => item.Inventory)
                .SingleAsync(item => item.Id == product.Id);
            Assert.Equal(12.5000m, persistedProduct.Inventory!.QuantityOnHand);
            Assert.Equal(DemoIdentity.OrganizationId, persistedProduct.OrganizationId);
        }
    }

    [Fact]
    public async Task InvoiceCannotReferenceUserFromAnotherOrganization()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
        var otherOrganizationId = Guid.NewGuid();
        dbContext.Organizations.Add(new Organization(otherOrganizationId, "Otra organización"));
        await dbContext.SaveChangesAsync();

        dbContext.Invoices.Add(new Invoice(
            Guid.NewGuid(),
            otherOrganizationId,
            DemoIdentity.UserId,
            "invoices/other/test.pdf",
            DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductSkuIsUniqueWithinOrganization()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
        dbContext.Products.AddRange(
            new Product(Guid.NewGuid(), DemoIdentity.OrganizationId, "SKU-1", "Producto A", "unidad"),
            new Product(Guid.NewGuid(), DemoIdentity.OrganizationId, "SKU-1", "Producto B", "unidad"));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
