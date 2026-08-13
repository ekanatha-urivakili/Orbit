using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintCompletionOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sprint_completion_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rollover_target_sprint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    processed_count = table.Column<int>(type: "integer", nullable: false),
                    total_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_completion_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_sprint_completion_operations_sprints_tenant_id_sprint_id",
                        columns: x => new { x.tenant_id, x.sprint_id },
                        principalTable: "sprints",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_completion_operations_tenant_id_sprint_id",
                table: "sprint_completion_operations",
                columns: new[] { "tenant_id", "sprint_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE sprint_completion_operations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sprint_completion_operations FORCE ROW LEVEL SECURITY;
                CREATE POLICY sprint_completion_operations_tenant_isolation ON sprint_completion_operations
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS sprint_completion_operations_tenant_isolation " +
                "ON sprint_completion_operations;");

            migrationBuilder.DropTable(
                name: "sprint_completion_operations");
        }
    }
}
