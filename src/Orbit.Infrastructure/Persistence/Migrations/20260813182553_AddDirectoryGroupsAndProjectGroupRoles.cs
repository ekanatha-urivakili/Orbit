using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryGroupsAndProjectGroupRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "directory_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_by_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_directory_groups", x => x.id);
                    table.UniqueConstraint("AK_directory_groups_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_directory_groups_tenant_memberships_tenant_id_created_by_me~",
                        columns: x => new { x.tenant_id, x.created_by_membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_memberships_directory_groups_tenant_id_group_id",
                        columns: x => new { x.tenant_id, x.group_id },
                        principalTable: "directory_groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_memberships_tenant_memberships_tenant_id_membership_id",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_group_role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_group_role_assignments", x => x.id);
                    table.CheckConstraint("ck_project_group_role_assignments_role", "project_role IN ('Administrator', 'Member', 'Viewer')");
                    table.ForeignKey(
                        name: "FK_project_group_role_assignments_directory_groups_tenant_id_g~",
                        columns: x => new { x.tenant_id, x.group_id },
                        principalTable: "directory_groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_group_role_assignments_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_directory_groups_tenant_id_created_by_membership_id",
                table: "directory_groups",
                columns: new[] { "tenant_id", "created_by_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_directory_groups_tenant_id_name",
                table: "directory_groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_tenant_id_group_id_membership_id",
                table: "group_memberships",
                columns: new[] { "tenant_id", "group_id", "membership_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_tenant_id_membership_id",
                table: "group_memberships",
                columns: new[] { "tenant_id", "membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_project_group_role_assignments_tenant_id_group_id",
                table: "project_group_role_assignments",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_project_group_role_assignments_tenant_id_project_id_group_id",
                table: "project_group_role_assignments",
                columns: new[] { "tenant_id", "project_id", "group_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE directory_groups ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directory_groups FORCE ROW LEVEL SECURITY;
                CREATE POLICY directory_groups_tenant_isolation ON directory_groups
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE group_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE group_memberships FORCE ROW LEVEL SECURITY;
                CREATE POLICY group_memberships_tenant_isolation ON group_memberships
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE project_group_role_assignments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE project_group_role_assignments FORCE ROW LEVEL SECURITY;
                CREATE POLICY project_group_role_assignments_tenant_isolation ON project_group_role_assignments
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS project_group_role_assignments_tenant_isolation " +
                "ON project_group_role_assignments;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS group_memberships_tenant_isolation ON group_memberships;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS directory_groups_tenant_isolation ON directory_groups;");

            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "project_group_role_assignments");

            migrationBuilder.DropTable(
                name: "directory_groups");
        }
    }
}
