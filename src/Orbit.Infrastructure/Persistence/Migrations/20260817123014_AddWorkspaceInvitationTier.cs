using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceInvitationTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tier",
                table: "workspace_invitations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_invitations_guest_role",
                table: "workspace_invitations",
                sql: "tier <> 'Guest' OR tenant_role = 'Member'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_invitations_tier",
                table: "workspace_invitations",
                sql: "tier IN ('Standard', 'Guest')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_invitations_guest_role",
                table: "workspace_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_invitations_tier",
                table: "workspace_invitations");

            migrationBuilder.DropColumn(
                name: "tier",
                table: "workspace_invitations");
        }
    }
}
