using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RioClaro.Infrastructure.Persistence;

#nullable disable

namespace RioClaro.Infrastructure.Migrations;

[DbContext(typeof(RioClaroDbContext))]
[Migration("20261015000000_AddInvoiceExtraction")]
public partial class AddInvoiceExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "Confidence",
            table: "InvoiceLines",
            type: "float",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentType",
            table: "Invoices",
            type: "varchar(100)",
            unicode: false,
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ConcurrencyToken",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<string>(
            name: "CustomerName",
            table: "Invoices",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerTaxId",
            table: "Invoices",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "DueDate",
            table: "Invoices",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DuplicateOfInvoiceId",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExtractionCompletedAtUtc",
            table: "Invoices",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExtractionAttemptCount",
            table: "Invoices",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ExtractionStatus",
            table: "Invoices",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExtractionStartedAtUtc",
            table: "Invoices",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FieldConfidenceJson",
            table: "Invoices",
            type: migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite"
                ? "TEXT"
                : "nvarchar(max)",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "InvoiceNumberNormalized",
            table: "Invoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastExtractionErrorCode",
            table: "Invoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginalFileName",
            table: "Invoices",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PageCount",
            table: "Invoices",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "PurchaseOrderNumber",
            table: "Invoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RetainUntilUtc",
            table: "Invoices",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Sha256",
            table: "Invoices",
            type: "varchar(64)",
            unicode: false,
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "SizeBytes",
            table: "Invoices",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "BlobETag",
            table: "Invoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierName",
            table: "Invoices",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierTaxId",
            table: "Invoices",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierTaxIdNormalized",
            table: "Invoices",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ValidationWarningsJson",
            table: "Invoices",
            type: migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite"
                ? "TEXT"
                : "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_Sha256",
            table: "Invoices",
            columns: new[] { "OrganizationId", "Sha256" },
            unique: true,
            filter: "[Sha256] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_SupplierTaxIdNormalized_InvoiceNumberNormalized_IssueDate_Total",
            table: "Invoices",
            columns: new[]
            {
                "OrganizationId",
                "SupplierTaxIdNormalized",
                "InvoiceNumberNormalized",
                "IssueDate",
                "Total"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("Confidence", "InvoiceLines");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_OrganizationId_Sha256",
            table: "Invoices");
        migrationBuilder.DropIndex(
            name: "IX_Invoices_OrganizationId_SupplierTaxIdNormalized_InvoiceNumberNormalized_IssueDate_Total",
            table: "Invoices");

        migrationBuilder.DropColumn("BlobETag", "Invoices");
        migrationBuilder.DropColumn("ConcurrencyToken", "Invoices");
        migrationBuilder.DropColumn("ContentType", "Invoices");
        migrationBuilder.DropColumn("CustomerName", "Invoices");
        migrationBuilder.DropColumn("CustomerTaxId", "Invoices");
        migrationBuilder.DropColumn("DueDate", "Invoices");
        migrationBuilder.DropColumn("DuplicateOfInvoiceId", "Invoices");
        migrationBuilder.DropColumn("ExtractionAttemptCount", "Invoices");
        migrationBuilder.DropColumn("ExtractionCompletedAtUtc", "Invoices");
        migrationBuilder.DropColumn("ExtractionStartedAtUtc", "Invoices");
        migrationBuilder.DropColumn("ExtractionStatus", "Invoices");
        migrationBuilder.DropColumn("FieldConfidenceJson", "Invoices");
        migrationBuilder.DropColumn("InvoiceNumberNormalized", "Invoices");
        migrationBuilder.DropColumn("LastExtractionErrorCode", "Invoices");
        migrationBuilder.DropColumn("OriginalFileName", "Invoices");
        migrationBuilder.DropColumn("PageCount", "Invoices");
        migrationBuilder.DropColumn("PurchaseOrderNumber", "Invoices");
        migrationBuilder.DropColumn("RetainUntilUtc", "Invoices");
        migrationBuilder.DropColumn("Sha256", "Invoices");
        migrationBuilder.DropColumn("SizeBytes", "Invoices");
        migrationBuilder.DropColumn("SupplierName", "Invoices");
        migrationBuilder.DropColumn("SupplierTaxId", "Invoices");
        migrationBuilder.DropColumn("SupplierTaxIdNormalized", "Invoices");
        migrationBuilder.DropColumn("ValidationWarningsJson", "Invoices");
    }
}
