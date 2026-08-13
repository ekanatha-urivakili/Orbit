using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMembershipAndProjectRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    principal_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => x.id);
                    table.UniqueConstraint("AK_tenant_memberships_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_tenant_memberships_principal_type", "principal_type IN ('User', 'ServiceAccount')");
                    table.CheckConstraint("ck_tenant_memberships_role", "tenant_role IN ('Owner', 'Administrator', 'Member')");
                });

            migrationBuilder.CreateTable(
                name: "project_role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_role_assignments", x => x.id);
                    table.CheckConstraint("ck_project_role_assignments_role", "project_role IN ('Administrator', 'Member', 'Viewer')");
                    table.ForeignKey(
                        name: "FK_project_role_assignments_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_role_assignments_tenant_memberships_tenant_id_membe~",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_role_assignments_tenant_id_membership_id",
                table: "project_role_assignments",
                columns: new[] { "tenant_id", "membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_project_role_assignments_tenant_id_project_id_membership_id",
                table: "project_role_assignments",
                columns: new[] { "tenant_id", "project_id", "membership_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id_issuer_subject",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "issuer", "subject" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE tenant_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_memberships FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_memberships_tenant_isolation ON tenant_memberships
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE project_role_assignments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE project_role_assignments FORCE ROW LEVEL SECURITY;
                CREATE POLICY project_role_assignments_tenant_isolation ON project_role_assignments
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_role_assignments");

            migrationBuilder.DropTable(
                name: "tenant_memberships");
        }
    }
}
