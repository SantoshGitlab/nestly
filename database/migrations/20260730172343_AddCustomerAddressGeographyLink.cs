using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAddressGeographyLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "locality_id",
                table: "customer_address",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pincode_id",
                table: "customer_address",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_address_locality_id",
                table: "customer_address",
                column: "locality_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_address_pincode_id",
                table: "customer_address",
                column: "pincode_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customer_address_localities_locality_id",
                table: "customer_address",
                column: "locality_id",
                principalTable: "locality",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_customer_address_pincodes_pincode_id",
                table: "customer_address",
                column: "pincode_id",
                principalTable: "pincode",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customer_address_localities_locality_id",
                table: "customer_address");

            migrationBuilder.DropForeignKey(
                name: "fk_customer_address_pincodes_pincode_id",
                table: "customer_address");

            migrationBuilder.DropIndex(
                name: "ix_customer_address_locality_id",
                table: "customer_address");

            migrationBuilder.DropIndex(
                name: "ix_customer_address_pincode_id",
                table: "customer_address");

            migrationBuilder.DropColumn(
                name: "locality_id",
                table: "customer_address");

            migrationBuilder.DropColumn(
                name: "pincode_id",
                table: "customer_address");
        }
    }
}
