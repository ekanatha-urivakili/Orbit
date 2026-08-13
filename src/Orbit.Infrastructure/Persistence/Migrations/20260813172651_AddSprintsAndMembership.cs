using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintsAndMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sprints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    goal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprints", x => x.id);
                    table.UniqueConstraint("AK_sprints_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_sprints_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_sprint_memberships_sprints_tenant_id_sprint_id",
                        columns: x => new { x.tenant_id, x.sprint_id },
                        principalTable: "sprints",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sprint_memberships_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_memberships_tenant_id_sprint_id_removed_at",
                table: "sprint_memberships",
                columns: new[] { "tenant_id", "sprint_id", "removed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_memberships_tenant_id_work_item_id_removed_at",
                table: "sprint_memberships",
                columns: new[] { "tenant_id", "work_item_id", "removed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sprints_tenant_id_project_id",
                table: "sprints",
                columns: new[] { "tenant_id", "project_id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE sprints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sprints FORCE ROW LEVEL SECURITY;
                CREATE POLICY sprints_tenant_isolation ON sprints
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE sprint_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sprint_memberships FORCE ROW LEVEL SECURITY;
                CREATE POLICY sprint_memberships_tenant_isolation ON sprint_memberships
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS sprint_memberships_tenant_isolation ON sprint_memberships;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS sprints_tenant_isolation ON sprints;");

            migrationBuilder.DropTable(
                name: "sprint_memberships");

            migrationBuilder.DropTable(
                name: "sprints");
        }
    }
}
