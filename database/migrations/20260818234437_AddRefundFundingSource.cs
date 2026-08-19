using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundFundingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "payment_transaction_id",
                table: "refund_transaction",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Every refund that exists today is payment-funded: the column it
            // discriminates (payment_transaction_id) was NOT NULL until the
            // AlterColumn above, so a wallet-funded refund could not have been
            // recorded. Backfilled through the column default, the same way
            // AddServiceCatalogManagementFields backfilled price_type.
            migrationBuilder.AddColumn<string>(
                name: "funding_source",
                table: "refund_transaction",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Payment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "funding_source",
                table: "refund_transaction");

            migrationBuilder.AlterColumn<Guid>(
                name: "payment_transaction_id",
                table: "refund_transaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
