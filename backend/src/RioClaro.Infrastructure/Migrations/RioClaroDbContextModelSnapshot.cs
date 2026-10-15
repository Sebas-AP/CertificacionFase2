using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using RioClaro.Infrastructure.Persistence;

#nullable disable

namespace RioClaro.Infrastructure.Migrations;

[DbContext(typeof(RioClaroDbContext))]
partial class RioClaroDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.22")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("RioClaro.Domain.Entities.Organization", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.HasKey("Id");
            entity.ToTable("Organizations");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.ApplicationUser", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            entity.HasKey("Id");

            entity.HasAlternateKey("OrganizationId", "Id");

            entity.HasIndex("OrganizationId");

            entity.ToTable("Users");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Supplier", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("TaxIdentifier")
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            entity.HasKey("Id");

            entity.HasAlternateKey("OrganizationId", "Id");

            entity.HasIndex("OrganizationId", "Name");

            entity.ToTable("Suppliers");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Invoice", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("CurrencyCode")
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasColumnType("varchar(3)");

            entity.Property<string>("DocumentStorageKey")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("nvarchar(512)");

            entity.Property<Guid?>("SupplierId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("InvoiceNumber")
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<DateOnly?>("IssueDate")
                .HasColumnType("date");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<decimal?>("Subtotal")
                .HasPrecision(19, 4)
                .HasColumnType("decimal(19,4)");

            entity.Property<decimal?>("TaxAmount")
                .HasPrecision(19, 4)
                .HasColumnType("decimal(19,4)");

            entity.Property<decimal?>("Total")
                .HasPrecision(19, 4)
                .HasColumnType("decimal(19,4)");

            entity.Property<int>("Status")
                .HasColumnType("int");

            entity.Property<DateTimeOffset>("UploadedAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<Guid>("UploadedByUserId")
                .HasColumnType("uniqueidentifier");

            entity.HasKey("Id");

            entity.HasAlternateKey("OrganizationId", "Id");

            entity.HasIndex("OrganizationId", "InvoiceNumber");

            entity.HasIndex("OrganizationId", "Status", "UploadedAtUtc");

            entity.HasIndex("OrganizationId", "SupplierId");

            entity.HasIndex("OrganizationId", "UploadedByUserId");

            entity.ToTable("Invoices");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InvoiceLine", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Description")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("nvarchar(500)");

            entity.Property<Guid>("InvoiceId")
                .HasColumnType("uniqueidentifier");

            entity.Property<decimal>("LineTotal")
                .HasPrecision(19, 4)
                .HasColumnType("decimal(19,4)");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<decimal>("Quantity")
                .HasPrecision(18, 4)
                .HasColumnType("decimal(18,4)");

            entity.Property<decimal>("UnitPrice")
                .HasPrecision(19, 4)
                .HasColumnType("decimal(19,4)");

            entity.HasKey("Id");

            entity.HasIndex("OrganizationId", "InvoiceId");

            entity.ToTable("InvoiceLines");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InvoiceReviewHistory", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Action")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            entity.Property<string>("DetailsJson")
                .IsRequired()
                .HasMaxLength(2000)
                .HasColumnType("nvarchar(2000)");

            entity.Property<Guid>("InvoiceId")
                .HasColumnType("uniqueidentifier");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<DateTimeOffset>("OccurredAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<Guid>("PerformedByUserId")
                .HasColumnType("uniqueidentifier");

            entity.HasKey("Id");

            entity.HasIndex("OrganizationId", "InvoiceId", "OccurredAtUtc");

            entity.HasIndex("OrganizationId", "PerformedByUserId");

            entity.ToTable("InvoiceReviewHistory");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Product", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Sku")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("Unit")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            entity.HasKey("Id");

            entity.HasAlternateKey("OrganizationId", "Id");

            entity.HasIndex("OrganizationId", "Sku")
                .IsUnique();

            entity.ToTable("Products");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InventoryItem", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<Guid>("ProductId")
                .HasColumnType("uniqueidentifier");

            entity.Property<decimal>("QuantityOnHand")
                .HasPrecision(18, 4)
                .HasColumnType("decimal(18,4)");

            entity.Property<decimal>("ReorderThreshold")
                .HasPrecision(18, 4)
                .HasColumnType("decimal(18,4)");

            entity.HasKey("Id");

            entity.HasIndex("OrganizationId", "ProductId")
                .IsUnique();

            entity.ToTable("InventoryItems");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.ApplicationUser", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Organization", "Organization")
                .WithMany("Users")
                .HasForeignKey("OrganizationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Organization");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Supplier", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Organization", "Organization")
                .WithMany("Suppliers")
                .HasForeignKey("OrganizationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Organization");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Invoice", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Organization", "Organization")
                .WithMany("Invoices")
                .HasForeignKey("OrganizationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.HasOne("RioClaro.Domain.Entities.Supplier", "Supplier")
                .WithMany("Invoices")
                .HasForeignKey("OrganizationId", "SupplierId")
                .HasPrincipalKey("OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne("RioClaro.Domain.Entities.ApplicationUser", "UploadedByUser")
                .WithMany("UploadedInvoices")
                .HasForeignKey("OrganizationId", "UploadedByUserId")
                .HasPrincipalKey("OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Organization");
            entity.Navigation("Supplier");
            entity.Navigation("UploadedByUser");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InvoiceLine", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Invoice", "Invoice")
                .WithMany("Lines")
                .HasForeignKey("OrganizationId", "InvoiceId")
                .HasPrincipalKey("OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Invoice");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InvoiceReviewHistory", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Invoice", "Invoice")
                .WithMany("ReviewHistory")
                .HasForeignKey("OrganizationId", "InvoiceId")
                .HasPrincipalKey("OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasOne("RioClaro.Domain.Entities.ApplicationUser", "PerformedByUser")
                .WithMany()
                .HasForeignKey("OrganizationId", "PerformedByUserId")
                .HasPrincipalKey("OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Invoice");
            entity.Navigation("PerformedByUser");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Product", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Organization", "Organization")
                .WithMany("Products")
                .HasForeignKey("OrganizationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Organization");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.InventoryItem", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Product", "Product")
                .WithOne("Inventory")
                .HasForeignKey("RioClaro.Domain.Entities.InventoryItem", "OrganizationId", "ProductId")
                .HasPrincipalKey("RioClaro.Domain.Entities.Product", "OrganizationId", "Id")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Product");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Organization", entity =>
        {
            entity.Navigation("Users");
            entity.Navigation("Suppliers");
            entity.Navigation("Invoices");
            entity.Navigation("Products");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.ApplicationUser", entity =>
        {
            entity.Navigation("UploadedInvoices");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Supplier", entity =>
        {
            entity.Navigation("Invoices");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Invoice", entity =>
        {
            entity.Navigation("Lines");
            entity.Navigation("ReviewHistory");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Product", entity =>
        {
            entity.Navigation("Inventory");
        });
    }
}
