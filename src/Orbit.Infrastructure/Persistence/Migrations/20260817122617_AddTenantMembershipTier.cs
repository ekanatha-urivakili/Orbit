using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMembershipTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tier",
                table: "tenant_memberships",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_memberships_guest_role",
                table: "tenant_memberships",
                sql: "tier <> 'Guest' OR tenant_role = 'Member'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_memberships_tier",
                table: "tenant_memberships",
                sql: "tier IN ('Standard', 'Guest')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_memberships_guest_role",
                table: "tenant_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_memberships_tier",
                table: "tenant_memberships");

            migrationBuilder.DropColumn(
                name: "tier",
                table: "tenant_memberships");
        }
    }
}
