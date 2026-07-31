using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsPagesBannersFaqsMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cms_faq",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    publish_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_faq", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cms_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_media", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cms_page",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    seo_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seo_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    seo_keywords = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    publish_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_page", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "banner",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    publish_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banner", x => x.id);
                    table.ForeignKey(
                        name: "fk_banner_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_banner_cms_media_assets_media_id",
                        column: x => x.media_id,
                        principalTable: "cms_media",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_banner_category_id",
                table: "banner",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_banner_media_id",
                table: "banner",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_banner_placement_status_sort_order",
                table: "banner",
                columns: new[] { "placement", "status", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_faq_placement_status_sort_order",
                table: "cms_faq",
                columns: new[] { "placement", "status", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_page_slug",
                table: "cms_page",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_page_status_placement",
                table: "cms_page",
                columns: new[] { "status", "placement" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banner");

            migrationBuilder.DropTable(
                name: "cms_faq");

            migrationBuilder.DropTable(
                name: "cms_page");

            migrationBuilder.DropTable(
                name: "cms_media");
        }
    }
}
