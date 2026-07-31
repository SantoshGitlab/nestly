using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "export_job",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    result_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    result_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_job", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_export_job_requested_by_admin_user_id",
                table: "export_job",
                column: "requested_by_admin_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "export_job");
        }
    }
}
