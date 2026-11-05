using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RioClaro.Infrastructure.Persistence;

#nullable disable

namespace RioClaro.Infrastructure.Migrations;

[DbContext(typeof(RioClaroDbContext))]
[Migration("20261016000000_AddInvoiceRetentionControls")]
public partial class AddInvoiceRetentionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "LegalHold",
            table: "Invoices",
            type: migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite" ? "INTEGER" : "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PdfDeletedAtUtc",
            table: "Invoices",
            type: migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite"
                ? "INTEGER"
                : "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_LegalHold_PdfDeletedAtUtc_RetainUntilUtc",
            table: "Invoices",
            columns: new[] { "LegalHold", "PdfDeletedAtUtc", "RetainUntilUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Invoices_LegalHold_PdfDeletedAtUtc_RetainUntilUtc",
            table: "Invoices");

        migrationBuilder.DropColumn("LegalHold", "Invoices");
        migrationBuilder.DropColumn("PdfDeletedAtUtc", "Invoices");
    }
}
