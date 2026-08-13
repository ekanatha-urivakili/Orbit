using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "board_columns",
                columns: table => new
                {
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    wip_limit = table.Column<int>(type: "integer", nullable: true),
                    wip_limit_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board_columns", x => new { x.tenant_id, x.project_id, x.status });
                    table.ForeignKey(
                        name: "FK_board_columns_boards_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "boards",
                        principalColumns: new[] { "tenant_id", "project_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                ALTER TABLE board_columns ENABLE ROW LEVEL SECURITY;
                ALTER TABLE board_columns FORCE ROW LEVEL SECURITY;
                CREATE POLICY board_columns_tenant_isolation ON board_columns
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS board_columns_tenant_isolation ON board_columns;");

            migrationBuilder.DropTable(
                name: "board_columns");
        }
    }
}
