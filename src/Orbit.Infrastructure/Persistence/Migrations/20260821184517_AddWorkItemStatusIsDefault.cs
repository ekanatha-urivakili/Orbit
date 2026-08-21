using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStatusIsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "work_item_status_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: every existing project's lowest-Order status becomes its explicit default,
            // preserving exactly the behaviour GetDefaultAsync used to derive implicitly from Order
            // before IsDefault existed, so no project's "status for a new work item" changes here.
            migrationBuilder.Sql("""
                WITH lowest_order_status AS (
                    SELECT DISTINCT ON (tenant_id, project_id) id
                    FROM work_item_status_definitions
                    ORDER BY tenant_id, project_id, "order"
                )
                UPDATE work_item_status_definitions
                SET is_default = true
                WHERE id IN (SELECT id FROM lowest_order_status);
                """);

            migrationBuilder.CreateIndex(
                name: "ux_work_item_status_definitions_one_default_per_project",
                table: "work_item_status_definitions",
                columns: new[] { "tenant_id", "project_id" },
                unique: true,
                filter: "is_default = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_work_item_status_definitions_one_default_per_project",
                table: "work_item_status_definitions");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "work_item_status_definitions");
        }
    }
}
