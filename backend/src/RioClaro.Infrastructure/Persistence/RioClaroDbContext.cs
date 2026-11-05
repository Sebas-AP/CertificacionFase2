using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RioClaro.Domain.Entities;

namespace RioClaro.Infrastructure.Persistence;

public sealed class RioClaroDbContext(DbContextOptions<RioClaroDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceReviewHistory> InvoiceReviewHistory => Set<InvoiceReviewHistory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(user => user.Role)
                .HasMaxLength(50)
                .IsRequired();
            entity.HasOne(user => user.Organization)
                .WithMany(organization => organization.Users)
                .HasForeignKey(user => user.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasAlternateKey(user => new { user.OrganizationId, user.Id });
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.HasKey(supplier => supplier.Id);
            entity.Property(supplier => supplier.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(supplier => supplier.TaxIdentifier)
                .HasMaxLength(50);
            entity.HasAlternateKey(supplier => new { supplier.OrganizationId, supplier.Id });
            entity.HasIndex(supplier => new { supplier.OrganizationId, supplier.Name });
            entity.HasOne(supplier => supplier.Organization)
                .WithMany(organization => organization.Suppliers)
                .HasForeignKey(supplier => supplier.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                entity.Property(invoice => invoice.UploadedAtUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset, long>(
                        value => value.UtcTicks,
                        value => new DateTimeOffset(value, TimeSpan.Zero)));
                entity.Property(invoice => invoice.RetainUntilUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset?, long?>(
                        value => value.HasValue ? value.Value.UtcTicks : null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null));
                entity.Property(invoice => invoice.PdfDeletedAtUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset?, long?>(
                        value => value.HasValue ? value.Value.UtcTicks : null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null));
                entity.Property(invoice => invoice.ExtractionStartedAtUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset?, long?>(
                        value => value.HasValue ? value.Value.UtcTicks : null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null));
                entity.Property(invoice => invoice.ExtractionCompletedAtUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset?, long?>(
                        value => value.HasValue ? value.Value.UtcTicks : null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null));
            }

            entity.ToTable("Invoices");
            entity.HasKey(invoice => invoice.Id);
            entity.HasAlternateKey(invoice => new { invoice.OrganizationId, invoice.Id });
            entity.Property(invoice => invoice.DocumentStorageKey)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(invoice => invoice.Status)
                .HasConversion<int>();
            entity.Property(invoice => invoice.InvoiceNumber)
                .HasMaxLength(100);
            entity.Property(invoice => invoice.InvoiceNumberNormalized)
                .HasMaxLength(100);
            entity.Property(invoice => invoice.SupplierName)
                .HasMaxLength(200);
            entity.Property(invoice => invoice.SupplierTaxId)
                .HasMaxLength(50);
            entity.Property(invoice => invoice.SupplierTaxIdNormalized)
                .HasMaxLength(50);
            entity.Property(invoice => invoice.CustomerName)
                .HasMaxLength(200);
            entity.Property(invoice => invoice.CustomerTaxId)
                .HasMaxLength(50);
            entity.Property(invoice => invoice.PurchaseOrderNumber)
                .HasMaxLength(100);
            entity.Property(invoice => invoice.CurrencyCode)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(invoice => invoice.OriginalFileName)
                .HasMaxLength(255);
            entity.Property(invoice => invoice.ContentType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(invoice => invoice.Sha256)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(invoice => invoice.BlobETag)
                .HasMaxLength(100);
            entity.Property(invoice => invoice.LegalHold)
                .HasDefaultValue(false);
            entity.Property(invoice => invoice.LastExtractionErrorCode)
                .HasMaxLength(100);
            entity.HasIndex(invoice => new
            {
                invoice.LegalHold,
                invoice.PdfDeletedAtUtc,
                invoice.RetainUntilUtc
            });
            entity.Property(invoice => invoice.ExtractionStatus)
                .HasConversion<int>();
            entity.Property(invoice => invoice.FieldConfidenceJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();
            entity.Property(invoice => invoice.ValidationWarningsJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();
            entity.Property(invoice => invoice.ConcurrencyToken)
                .ValueGeneratedNever()
                .IsConcurrencyToken();
            entity.Property(invoice => invoice.Subtotal).HasPrecision(19, 4);
            entity.Property(invoice => invoice.TaxAmount).HasPrecision(19, 4);
            entity.Property(invoice => invoice.Total).HasPrecision(19, 4);
            entity.HasIndex(invoice => new
            {
                invoice.OrganizationId,
                invoice.Status,
                invoice.UploadedAtUtc
            });
            entity.HasIndex(invoice => new { invoice.OrganizationId, invoice.InvoiceNumber });
            entity.HasIndex(invoice => new { invoice.OrganizationId, invoice.Sha256 })
                .IsUnique()
                .HasFilter("[Sha256] IS NOT NULL");
            entity.HasIndex(invoice => new
            {
                invoice.OrganizationId,
                invoice.SupplierTaxIdNormalized,
                invoice.InvoiceNumberNormalized,
                invoice.IssueDate,
                invoice.Total
            });
            entity.HasOne(invoice => invoice.Organization)
                .WithMany(organization => organization.Invoices)
                .HasForeignKey(invoice => invoice.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(invoice => invoice.UploadedByUser)
                .WithMany(user => user.UploadedInvoices)
                .HasForeignKey(invoice => new { invoice.OrganizationId, invoice.UploadedByUserId })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(invoice => invoice.Supplier)
                .WithMany(supplier => supplier.Invoices)
                .HasForeignKey(invoice => new { invoice.OrganizationId, invoice.SupplierId })
                .HasPrincipalKey(supplier => new { supplier.OrganizationId, supplier.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).ValueGeneratedNever();
            entity.Property(line => line.Description)
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(line => line.Quantity).HasPrecision(18, 4);
            entity.Property(line => line.UnitPrice).HasPrecision(19, 4);
            entity.Property(line => line.LineTotal).HasPrecision(19, 4);
            entity.Property(line => line.Confidence).HasColumnType("float");
            entity.HasOne(line => line.Invoice)
                .WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => new { line.OrganizationId, line.InvoiceId })
                .HasPrincipalKey(invoice => new { invoice.OrganizationId, invoice.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceReviewHistory>(entity =>
        {
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                entity.Property(history => history.OccurredAtUtc)
                    .HasConversion(new ValueConverter<DateTimeOffset, long>(
                        value => value.UtcTicks,
                        value => new DateTimeOffset(value, TimeSpan.Zero)));
            }

            entity.ToTable("InvoiceReviewHistory");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Action)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(history => history.DetailsJson)
                .HasMaxLength(2000)
                .IsRequired();
            entity.HasIndex(history => new
            {
                history.OrganizationId,
                history.InvoiceId,
                history.OccurredAtUtc
            });
            entity.HasOne(history => history.Invoice)
                .WithMany(invoice => invoice.ReviewHistory)
                .HasForeignKey(history => new { history.OrganizationId, history.InvoiceId })
                .HasPrincipalKey(invoice => new { invoice.OrganizationId, invoice.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(history => history.PerformedByUser)
                .WithMany()
                .HasForeignKey(history => new { history.OrganizationId, history.PerformedByUserId })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Sku)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(product => product.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(product => product.Unit)
                .HasMaxLength(30)
                .IsRequired();
            entity.HasAlternateKey(product => new { product.OrganizationId, product.Id });
            entity.HasIndex(product => new { product.OrganizationId, product.Sku }).IsUnique();
            entity.HasOne(product => product.Organization)
                .WithMany(organization => organization.Products)
                .HasForeignKey(product => product.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.QuantityOnHand).HasPrecision(18, 4);
            entity.Property(item => item.ReorderThreshold).HasPrecision(18, 4);
            entity.HasIndex(item => new { item.OrganizationId, item.ProductId }).IsUnique();
            entity.HasOne(item => item.Product)
                .WithOne(product => product.Inventory)
                .HasForeignKey<InventoryItem>(item => new { item.OrganizationId, item.ProductId })
                .HasPrincipalKey<Product>(product => new { product.OrganizationId, product.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
