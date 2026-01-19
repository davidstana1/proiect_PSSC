using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProiectPSSC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeInvoices_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "amount",
                table: "invoices");

            migrationBuilder.AddColumn<string>(
                name: "billing_email",
                table: "invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "currency",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "due_date",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "number",
                table: "invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill existing rows with a unique invoice number before we add a UNIQUE index.
            // This avoids 23505 errors if the database already contains invoices.
            migrationBuilder.Sql(@"
UPDATE invoices
SET number = 'INV-MIGR-' || replace(id::text,'-',''),
    billing_email = COALESCE(NULLIF(billing_email, ''), 'unknown@example.com'),
    due_date = CASE WHEN due_date = '0001-01-01 00:00:00+00'::timestamptz THEN (now() + interval '14 days') ELSE due_date END
WHERE number IS NULL OR number = '' OR billing_email = '' OR due_date = '0001-01-01 00:00:00+00'::timestamptz;
");

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    product_code = table.Column<string>(type: "text", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => new { x.invoice_id, x.product_code });
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_number",
                table: "invoices",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "IX_invoices_number",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "billing_email",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "number",
                table: "invoices");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
