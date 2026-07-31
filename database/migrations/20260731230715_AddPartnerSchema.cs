using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partner",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    partner_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    onboarding_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_auth_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_auth_identity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_login_attempt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_login_attempt", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_otp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_otp", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_session",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    device_info = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_session", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_availability_window",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_availability_window", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_availability_window_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_blackout_date",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_blackout_date", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_blackout_date_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_capacity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_jobs_per_day = table.Column<int>(type: "integer", nullable: true),
                    max_jobs_per_slot = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_capacity", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_capacity_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_kyc_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_ref = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    verification_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_kyc_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_kyc_document_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_service_area",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pincode_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_service_area", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_service_area_city_city_id",
                        column: x => x.city_id,
                        principalTable: "city",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_service_area_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_service_area_pincodes_pincode_id",
                        column: x => x.pincode_id,
                        principalTable: "pincode",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_service_area_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_skill_mapping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_skill_mapping", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_skill_mapping_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_skill_mapping_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partner_skill_mapping_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_partner_phone",
                table: "partner",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_auth_identity_partner_id",
                table: "partner_auth_identity",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_auth_identity_provider_identifier",
                table: "partner_auth_identity",
                columns: new[] { "provider", "identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_availability_window_partner_id_day_of_week",
                table: "partner_availability_window",
                columns: new[] { "partner_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_blackout_date_partner_id_start_date_end_date",
                table: "partner_blackout_date",
                columns: new[] { "partner_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_capacity_partner_id",
                table: "partner_capacity",
                column: "partner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_kyc_document_partner_id",
                table: "partner_kyc_document",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_login_attempt_identifier_occurred_at_utc",
                table: "partner_login_attempt",
                columns: new[] { "identifier", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_otp_partner_id",
                table: "partner_otp",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_otp_target",
                table: "partner_otp",
                column: "target");

            migrationBuilder.CreateIndex(
                name: "ix_partner_service_area_city_id",
                table: "partner_service_area",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_service_area_partner_id_city_id_zone_id_pincode_id",
                table: "partner_service_area",
                columns: new[] { "partner_id", "city_id", "zone_id", "pincode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_service_area_pincode_id",
                table: "partner_service_area",
                column: "pincode_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_service_area_zone_id",
                table: "partner_service_area",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_session_partner_id",
                table: "partner_session",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_session_refresh_token_hash",
                table: "partner_session",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_skill_mapping_category_id",
                table: "partner_skill_mapping",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_skill_mapping_partner_id_category_id_service_id",
                table: "partner_skill_mapping",
                columns: new[] { "partner_id", "category_id", "service_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_skill_mapping_service_id",
                table: "partner_skill_mapping",
                column: "service_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_auth_identity");

            migrationBuilder.DropTable(
                name: "partner_availability_window");

            migrationBuilder.DropTable(
                name: "partner_blackout_date");

            migrationBuilder.DropTable(
                name: "partner_capacity");

            migrationBuilder.DropTable(
                name: "partner_kyc_document");

            migrationBuilder.DropTable(
                name: "partner_login_attempt");

            migrationBuilder.DropTable(
                name: "partner_otp");

            migrationBuilder.DropTable(
                name: "partner_service_area");

            migrationBuilder.DropTable(
                name: "partner_session");

            migrationBuilder.DropTable(
                name: "partner_skill_mapping");

            migrationBuilder.DropTable(
                name: "partner");
        }
    }
}
