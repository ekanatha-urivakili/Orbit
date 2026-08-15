using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    tenant_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    invited_by_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_invitations", x => x.id);
                    table.UniqueConstraint("AK_workspace_invitations_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_workspace_invitations_role", "tenant_role IN ('Administrator', 'Member')");
                    table.CheckConstraint("ck_workspace_invitations_status", "status IN ('Active', 'Accepted', 'Revoked')");
                    table.CheckConstraint("ck_workspace_invitations_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_workspace_invitations_teams_tenant_id_team_id",
                        columns: x => new { x.tenant_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_tenant_memberships_tenant_id_invited_~",
                        columns: x => new { x.tenant_id, x.invited_by_membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_user_accounts_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_accepted_by_user_id",
                table: "workspace_invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_tenant_id_invited_by_membership_id",
                table: "workspace_invitations",
                columns: new[] { "tenant_id", "invited_by_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_tenant_id_normalized_email",
                table: "workspace_invitations",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_tenant_id_team_id",
                table: "workspace_invitations",
                columns: new[] { "tenant_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_token_hash",
                table: "workspace_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE workspace_invitations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE workspace_invitations FORCE ROW LEVEL SECURITY;
                CREATE POLICY workspace_invitations_tenant_isolation ON workspace_invitations
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_invitations");
        }
    }
}
