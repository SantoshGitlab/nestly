using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsMediaType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The "Image" default backfills every pre-existing cms_media row,
            // matching CmsMediaType's declaration order (Image = 0) so no
            // existing banner/media asset silently becomes a "video".
            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "cms_media",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Image");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "media_type",
                table: "cms_media");
        }
    }
}
