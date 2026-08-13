using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamDirectoryAndMembershipLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teams",
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
                    table.PrimaryKey("PK_teams", x => x.id);
                    table.UniqueConstraint("AK_teams_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_teams_tenant_memberships_tenant_id_created_by_membership_id",
                        columns: x => new { x.tenant_id, x.created_by_membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_memberships_teams_tenant_id_team_id",
                        columns: x => new { x.tenant_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_memberships_tenant_memberships_tenant_id_membership_id",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_tenant_id_membership_id",
                table: "team_memberships",
                columns: new[] { "tenant_id", "membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_tenant_id_team_id_membership_id",
                table: "team_memberships",
                columns: new[] { "tenant_id", "team_id", "membership_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_tenant_id_created_by_membership_id",
                table: "teams",
                columns: new[] { "tenant_id", "created_by_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_teams_tenant_id_name",
                table: "teams",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE teams ENABLE ROW LEVEL SECURITY;
                ALTER TABLE teams FORCE ROW LEVEL SECURITY;
                CREATE POLICY teams_tenant_isolation ON teams
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE team_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE team_memberships FORCE ROW LEVEL SECURITY;
                CREATE POLICY team_memberships_tenant_isolation ON team_memberships
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                -- Login/refresh must discover a user's own memberships across every workspace before
                -- any workspace is selected, so no app.tenant_id is set for that lookup. Without this,
                -- tenant_memberships_tenant_isolation (USING tenant_id = app.tenant_id) hides every row
                -- once the runtime connects as a role that cannot BYPASSRLS (ADR-006/NFR-07), and login
                -- fails closed for every account. This additional permissive SELECT policy is combined
                -- with the tenant-isolation policy via OR and only ever exposes a membership row to the
                -- same user id set via app.principal_user_id by AuthenticationRepository - it grants no
                -- access to another tenant's data and does not apply to INSERT/UPDATE/DELETE.
                CREATE POLICY tenant_memberships_self_lookup ON tenant_memberships
                    FOR SELECT
                    USING (user_id IS NOT NULL
                        AND user_id = NULLIF(current_setting('app.principal_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_memberships_self_lookup ON tenant_memberships;");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "teams");
        }
    }
}
