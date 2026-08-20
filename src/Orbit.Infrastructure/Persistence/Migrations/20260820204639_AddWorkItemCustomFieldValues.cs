using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemCustomFieldValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_custom_field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_field_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    values = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_custom_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_item_custom_field_values_custom_field_definitions_tena~",
                        columns: x => new { x.tenant_id, x.custom_field_definition_id },
                        principalTable: "custom_field_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_item_custom_field_values_work_items_tenant_id_work_ite~",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_custom_field_values_tenant_id_custom_field_defini~",
                table: "work_item_custom_field_values",
                columns: new[] { "tenant_id", "custom_field_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ux_work_item_custom_field_values_tenant_item_field",
                table: "work_item_custom_field_values",
                columns: new[] { "tenant_id", "work_item_id", "custom_field_definition_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_custom_field_values ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_custom_field_values FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_custom_field_values_tenant_isolation ON work_item_custom_field_values
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS work_item_custom_field_values_tenant_isolation " +
                "ON work_item_custom_field_values;");

            migrationBuilder.DropTable(
                name: "work_item_custom_field_values");
        }
    }
}
