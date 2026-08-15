using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemTypeRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_type_definitions",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    color_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_type_definitions", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_work_item_type_definitions_order", "\"order\" BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_work_item_type_definitions_version", "version > 0");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO work_item_type_definitions
                    (tenant_id, id, label, description, "order", color_token, enabled, version, updated_at)
                SELECT tenant_source.tenant_id, seed.id, seed.label, seed.description, seed.sort_order,
                       seed.color_token, seed.enabled, 1, CURRENT_TIMESTAMP
                FROM (
                    SELECT id AS tenant_id FROM workspaces
                    UNION
                    SELECT tenant_id FROM projects
                    UNION
                    SELECT tenant_id FROM work_items
                    UNION
                    SELECT tenant_id FROM project_settings
                ) AS tenant_source
                CROSS JOIN (VALUES
                    ('Initiative', 'Initiative', 'A strategic outcome containing epics.', 10, 'lime', TRUE),
                    ('Epic', 'Epic', 'A large outcome spanning multiple work items.', 20, 'purple', TRUE),
                    ('Task', 'Task', 'A unit of implementation work.', 30, 'blue', TRUE),
                    ('Story', 'Story', 'User-visible product value.', 40, 'green', TRUE),
                    ('Bug', 'Bug', 'A defect in expected behaviour.', 50, 'red', TRUE),
                    ('Spike', 'Spike', 'Time-boxed research that reduces uncertainty.', 60, 'amber', TRUE),
                    ('Test', 'Test', 'A repeatable validation scenario.', 70, 'teal', TRUE),
                    ('Feature', 'Feature', 'A cohesive product capability.', 80, 'cyan', TRUE),
                    ('Request', 'Request', 'A request from a customer or stakeholder.', 90, 'orange', TRUE),
                    ('Subtask', 'Subtask', 'A historical child-work type.', 100, 'slate', FALSE)
                ) AS seed(id, label, description, sort_order, color_token, enabled);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_type",
                table: "work_items",
                columns: new[] { "tenant_id", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_project_settings_tenant_id_default_work_item_type",
                table: "project_settings",
                columns: new[] { "tenant_id", "default_work_item_type" });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_type_definitions_tenant_id_order",
                table: "work_item_type_definitions",
                columns: new[] { "tenant_id", "order" });

            migrationBuilder.AddForeignKey(
                name: "FK_project_settings_work_item_type_definitions_tenant_id_defau~",
                table: "project_settings",
                columns: new[] { "tenant_id", "default_work_item_type" },
                principalTable: "work_item_type_definitions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_work_item_type_definitions_tenant_id_type",
                table: "work_items",
                columns: new[] { "tenant_id", "type" },
                principalTable: "work_item_type_definitions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_type_definitions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_type_definitions FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_type_definitions_tenant_isolation ON work_item_type_definitions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_settings_work_item_type_definitions_tenant_id_defau~",
                table: "project_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_work_items_work_item_type_definitions_tenant_id_type",
                table: "work_items");

            migrationBuilder.DropTable(
                name: "work_item_type_definitions");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_type",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_project_settings_tenant_id_default_work_item_type",
                table: "project_settings");
        }
    }
}
