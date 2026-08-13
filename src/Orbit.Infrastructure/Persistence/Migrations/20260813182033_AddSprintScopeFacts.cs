using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintScopeFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sprint_scope_facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fact_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    estimate_delta = table.Column<decimal>(type: "numeric", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_scope_facts", x => x.id);
                    table.ForeignKey(
                        name: "FK_sprint_scope_facts_sprints_tenant_id_sprint_id",
                        columns: x => new { x.tenant_id, x.sprint_id },
                        principalTable: "sprints",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_scope_facts_tenant_id_sprint_id_occurred_at_id",
                table: "sprint_scope_facts",
                columns: new[] { "tenant_id", "sprint_id", "occurred_at", "id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE sprint_scope_facts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sprint_scope_facts FORCE ROW LEVEL SECURITY;
                CREATE POLICY sprint_scope_facts_tenant_isolation ON sprint_scope_facts
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS sprint_scope_facts_tenant_isolation ON sprint_scope_facts;");

            migrationBuilder.DropTable(
                name: "sprint_scope_facts");
        }
    }
}
