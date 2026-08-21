using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndRolePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "project_role_assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "project_group_role_assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission });
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every existing tenant (workspace) gets the 3 system roles that reproduce exactly what the
            // old hardcoded ProjectPermissionRoles.For switch granted, so this is a behavior-preserving
            // backfill, not a reset of anyone's access.
            migrationBuilder.Sql(
                """
                INSERT INTO roles (id, tenant_id, name, is_system, created_at)
                SELECT gen_random_uuid(), w.id, r.name, true, now()
                FROM workspaces w
                CROSS JOIN (VALUES ('Administrator'), ('Member'), ('Viewer')) AS r(name);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO role_permissions (role_id, permission)
                SELECT r.id, p.permission
                FROM roles r
                CROSS JOIN LATERAL unnest(
                    CASE r.name
                        WHEN 'Administrator' THEN ARRAY['View', 'CreateWorkItem', 'TransitionWorkItem', 'Administer']
                        WHEN 'Member' THEN ARRAY['View', 'CreateWorkItem', 'TransitionWorkItem']
                        WHEN 'Viewer' THEN ARRAY['View']
                    END
                ) AS p(permission);
                """);

            migrationBuilder.Sql(
                """
                UPDATE project_role_assignments a
                SET role_id = r.id
                FROM roles r
                WHERE r.tenant_id = a.tenant_id AND r.name = a.project_role;
                """);

            migrationBuilder.Sql(
                """
                UPDATE project_group_role_assignments a
                SET role_id = r.id
                FROM roles r
                WHERE r.tenant_id = a.tenant_id AND r.name = a.project_role;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_role_assignments_role",
                table: "project_role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_group_role_assignments_role",
                table: "project_group_role_assignments");

            migrationBuilder.DropColumn(
                name: "project_role",
                table: "project_role_assignments");

            migrationBuilder.DropColumn(
                name: "project_role",
                table: "project_group_role_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_project_role_assignments_role_id",
                table: "project_role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_group_role_assignments_role_id",
                table: "project_group_role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_tenant_id_name",
                table: "roles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_project_group_role_assignments_roles_role_id",
                table: "project_group_role_assignments",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_project_role_assignments_roles_role_id",
                table: "project_role_assignments",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_group_role_assignments_roles_role_id",
                table: "project_group_role_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_project_role_assignments_roles_role_id",
                table: "project_role_assignments");

            migrationBuilder.AddColumn<string>(
                name: "project_role",
                table: "project_role_assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "project_role",
                table: "project_group_role_assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE project_role_assignments a
                SET project_role = r.name
                FROM roles r
                WHERE r.id = a.role_id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE project_group_role_assignments a
                SET project_role = r.name
                FROM roles r
                WHERE r.id = a.role_id;
                """);

            migrationBuilder.DropIndex(
                name: "IX_project_role_assignments_role_id",
                table: "project_role_assignments");

            migrationBuilder.DropIndex(
                name: "IX_project_group_role_assignments_role_id",
                table: "project_group_role_assignments");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "project_role_assignments");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "project_group_role_assignments");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_role_assignments_role",
                table: "project_role_assignments",
                sql: "project_role IN ('Administrator', 'Member', 'Viewer')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_group_role_assignments_role",
                table: "project_group_role_assignments",
                sql: "project_role IN ('Administrator', 'Member', 'Viewer')");
        }
    }
}
