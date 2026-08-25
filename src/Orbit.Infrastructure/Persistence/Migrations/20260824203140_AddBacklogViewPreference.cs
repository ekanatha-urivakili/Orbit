using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacklogViewPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backlog_view_preferences",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    show_empty_sprints = table.Column<bool>(type: "boolean", nullable: false),
                    density = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hidden_fields = table.Column<string[]>(type: "text[]", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backlog_view_preferences", x => new { x.tenant_id, x.user_id, x.project_id });
                    table.ForeignKey(
                        name: "FK_backlog_view_preferences_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_backlog_view_preferences_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backlog_view_preferences_tenant_id_project_id",
                table: "backlog_view_preferences",
                columns: new[] { "tenant_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "IX_backlog_view_preferences_user_id",
                table: "backlog_view_preferences",
                column: "user_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE backlog_view_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE backlog_view_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY backlog_view_preferences_tenant_isolation ON backlog_view_preferences
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS backlog_view_preferences_tenant_isolation ON backlog_view_preferences;");

            migrationBuilder.DropTable(
                name: "backlog_view_preferences");
        }
    }
}
