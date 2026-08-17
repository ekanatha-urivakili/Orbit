using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "workspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.CheckConstraint("ck_organizations_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.id);
                    table.CheckConstraint("ck_organization_memberships_role", "role IN ('Owner', 'Administrator', 'Member')");
                    table.ForeignKey(
                        name: "FK_organization_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill: one organization per existing workspace, reusing the workspace's own id as
            // the organization's id (a clean 1:1 at this point in the schema's life) and its owner
            // membership(s) mirrored into organization_memberships, so existing installations keep
            // working under the new org-above-workspace model without manual intervention.
            migrationBuilder.Sql("""
                INSERT INTO organizations (id, slug, name, created_at)
                SELECT id, slug, name, created_at
                FROM workspaces;

                UPDATE workspaces
                SET organization_id = id;

                INSERT INTO organization_memberships (id, organization_id, user_id, role, created_at)
                SELECT gen_random_uuid(), tenant_id, user_id, 'Owner', created_at
                FROM tenant_memberships
                WHERE tenant_role = 'Owner' AND user_id IS NOT NULL AND is_active = TRUE;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "workspaces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_organization_id",
                table: "workspaces",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_organization_id_user_id",
                table: "organization_memberships",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workspaces_organizations_organization_id",
                table: "workspaces",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workspaces_organizations_organization_id",
                table: "workspaces");

            migrationBuilder.DropTable(
                name: "organization_memberships");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_workspaces_organization_id",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "workspaces");
        }
    }
}
