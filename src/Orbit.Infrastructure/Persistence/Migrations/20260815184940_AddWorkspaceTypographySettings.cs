using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceTypographySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_typography_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    left_font_family = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    left_font_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    left_font_size_px = table.Column<int>(type: "integer", nullable: false),
                    middle_font_family = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    middle_font_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    middle_font_size_px = table.Column<int>(type: "integer", nullable: false),
                    right_font_family = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    right_font_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    right_font_size_px = table.Column<int>(type: "integer", nullable: false),
                    control_height_px = table.Column<int>(type: "integer", nullable: false),
                    control_font_size_px = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_typography_settings", x => x.tenant_id);
                    table.ForeignKey(
                        name: "FK_workspace_typography_settings_workspaces_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_typography_settings");
        }
    }
}
