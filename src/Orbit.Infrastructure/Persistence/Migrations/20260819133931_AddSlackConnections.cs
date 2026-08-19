using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlackConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "slack_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    team_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    encrypted_webhook_url = table.Column<string>(type: "text", nullable: false),
                    connected_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_connections", x => x.id);
                    table.ForeignKey(
                        name: "FK_slack_connections_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_slack_connections_tenant_project",
                table: "slack_connections",
                columns: new[] { "tenant_id", "project_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE slack_connections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE slack_connections FORCE ROW LEVEL SECURITY;
                CREATE POLICY slack_connections_tenant_isolation ON slack_connections
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS slack_connections_tenant_isolation ON slack_connections;");

            migrationBuilder.DropTable(
                name: "slack_connections");
        }
    }
}
