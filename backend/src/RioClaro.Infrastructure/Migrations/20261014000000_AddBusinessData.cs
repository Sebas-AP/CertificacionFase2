using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RioClaro.Infrastructure.Persistence;

#nullable disable

namespace RioClaro.Infrastructure.Migrations;

[DbContext(typeof(RioClaroDbContext))]
[Migration("20261014000000_AddBusinessData")]
public partial class AddBusinessData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", product => product.Id);
                table.UniqueConstraint(
                    "AK_Products_OrganizationId_Id",
                    product => new { product.OrganizationId, product.Id });
                table.ForeignKey(
                    name: "FK_Products_Organizations_OrganizationId",
                    column: product => product.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TaxIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", supplier => supplier.Id);
                table.UniqueConstraint(
                    "AK_Suppliers_OrganizationId_Id",
                    supplier => new { supplier.OrganizationId, supplier.Id });
                table.ForeignKey(
                    name: "FK_Suppliers_Organizations_OrganizationId",
                    column: supplier => supplier.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InventoryItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                QuantityOnHand = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ReorderThreshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryItems", item => item.Id);
                table.ForeignKey(
                    name: "FK_InventoryItems_Products_OrganizationId_ProductId",
                    columns: item => new { item.OrganizationId, item.ProductId },
                    principalTable: "Products",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DocumentStorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                Subtotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", invoice => invoice.Id);
                table.UniqueConstraint(
                    "AK_Invoices_OrganizationId_Id",
                    invoice => new { invoice.OrganizationId, invoice.Id });
                table.ForeignKey(
                    name: "FK_Invoices_Organizations_OrganizationId",
                    column: invoice => invoice.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Invoices_Suppliers_OrganizationId_SupplierId",
                    columns: invoice => new { invoice.OrganizationId, invoice.SupplierId },
                    principalTable: "Suppliers",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Invoices_Users_OrganizationId_UploadedByUserId",
                    columns: invoice => new { invoice.OrganizationId, invoice.UploadedByUserId },
                    principalTable: "Users",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                LineTotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLines", line => line.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Invoices_OrganizationId_InvoiceId",
                    columns: line => new { line.OrganizationId, line.InvoiceId },
                    principalTable: "Invoices",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceReviewHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DetailsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceReviewHistory", history => history.Id);
                table.ForeignKey(
                    name: "FK_InvoiceReviewHistory_Invoices_OrganizationId_InvoiceId",
                    columns: history => new { history.OrganizationId, history.InvoiceId },
                    principalTable: "Invoices",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_InvoiceReviewHistory_Users_OrganizationId_PerformedByUserId",
                    columns: history => new { history.OrganizationId, history.PerformedByUserId },
                    principalTable: "Users",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Products_OrganizationId_Sku",
            table: "Products",
            columns: new[] { "OrganizationId", "Sku" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_OrganizationId_Id",
            table: "Users",
            columns: new[] { "OrganizationId", "Id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_OrganizationId_Name",
            table: "Suppliers",
            columns: new[] { "OrganizationId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_OrganizationId_ProductId",
            table: "InventoryItems",
            columns: new[] { "OrganizationId", "ProductId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_InvoiceNumber",
            table: "Invoices",
            columns: new[] { "OrganizationId", "InvoiceNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_Status_UploadedAtUtc",
            table: "Invoices",
            columns: new[] { "OrganizationId", "Status", "UploadedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_SupplierId",
            table: "Invoices",
            columns: new[] { "OrganizationId", "SupplierId" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_OrganizationId_UploadedByUserId",
            table: "Invoices",
            columns: new[] { "OrganizationId", "UploadedByUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_OrganizationId_InvoiceId",
            table: "InvoiceLines",
            columns: new[] { "OrganizationId", "InvoiceId" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceReviewHistory_OrganizationId_InvoiceId_OccurredAtUtc",
            table: "InvoiceReviewHistory",
            columns: new[] { "OrganizationId", "InvoiceId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceReviewHistory_OrganizationId_PerformedByUserId",
            table: "InvoiceReviewHistory",
            columns: new[] { "OrganizationId", "PerformedByUserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InvoiceReviewHistory");
        migrationBuilder.DropTable(name: "InventoryItems");
        migrationBuilder.DropTable(name: "InvoiceLines");
        migrationBuilder.DropTable(name: "Invoices");
        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "Suppliers");
        migrationBuilder.DropIndex(
            name: "IX_Users_OrganizationId_Id",
            table: "Users");
    }
}
