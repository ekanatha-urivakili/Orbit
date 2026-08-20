using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeCustomFieldsProjectScopedWithChoiceOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_custom_field_definitions",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_tenant_id_key",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_tenant_id_order",
                table: "custom_field_definitions");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "custom_field_definitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_custom_field_definitions",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "custom_field_choice_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_field_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_field_choice_options", x => new { x.tenant_id, x.custom_field_definition_id, x.id });
                    table.ForeignKey(
                        name: "FK_custom_field_choice_options_custom_field_definitions_tenant~",
                        columns: x => new { x.tenant_id, x.custom_field_definition_id },
                        principalTable: "custom_field_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_tenant_id_project_id_key",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "project_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_tenant_id_project_id_order",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "project_id", "order" });

            migrationBuilder.AddForeignKey(
                name: "FK_custom_field_definitions_projects_tenant_id_project_id",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "project_id" },
                principalTable: "projects",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                """
                ALTER TABLE custom_field_choice_options ENABLE ROW LEVEL SECURITY;
                ALTER TABLE custom_field_choice_options FORCE ROW LEVEL SECURITY;
                CREATE POLICY custom_field_choice_options_tenant_isolation ON custom_field_choice_options
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS custom_field_choice_options_tenant_isolation ON custom_field_choice_options;");

            migrationBuilder.DropForeignKey(
                name: "FK_custom_field_definitions_projects_tenant_id_project_id",
                table: "custom_field_definitions");

            migrationBuilder.DropTable(
                name: "custom_field_choice_options");

            migrationBuilder.DropPrimaryKey(
                name: "PK_custom_field_definitions",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_tenant_id_project_id_key",
                table: "custom_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_custom_field_definitions_tenant_id_project_id_order",
                table: "custom_field_definitions");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "custom_field_definitions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_custom_field_definitions",
                table: "custom_field_definitions",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_tenant_id_key",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_tenant_id_order",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "order" });
        }
    }
}
