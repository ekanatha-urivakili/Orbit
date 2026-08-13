using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalBootstrapAndSystemTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items");

            migrationBuilder.AlterColumn<string>(
                name: "subject",
                table: "tenant_memberships",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "issuer",
                table: "tenant_memberships",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "tenant_memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.id);
                    table.CheckConstraint("ck_user_accounts_status", "status IN ('Active', 'Disabled')");
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    authorization_epoch = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.CheckConstraint("ck_workspaces_authorization_epoch", "authorization_epoch > 0");
                    table.CheckConstraint("ck_workspaces_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "local_credentials",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    hash_algorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hash_parameters_version = table.Column<int>(type: "integer", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_credentials", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_local_credentials_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_role_assignments",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_role_assignments", x => new { x.user_id, x.site_role });
                    table.CheckConstraint("ck_site_role_assignments_role", "site_role = 'SuperAdministrator'");
                    table.ForeignKey(
                        name: "FK_site_role_assignments_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items",
                sql: "type IN ('Epic', 'Task', 'Story', 'Feature', 'Request', 'Bug', 'Subtask')");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id_user_id",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_user_id",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_memberships_identity",
                table: "tenant_memberships",
                sql: "(user_id IS NOT NULL AND issuer IS NULL AND subject IS NULL AND principal_type = 'User') OR (user_id IS NULL AND issuer IS NOT NULL AND subject IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_normalized_email",
                table: "user_accounts",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_slug",
                table: "workspaces",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_memberships_user_accounts_user_id",
                table: "tenant_memberships",
                column: "user_id",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_memberships_user_accounts_user_id",
                table: "tenant_memberships");

            migrationBuilder.DropTable(
                name: "local_credentials");

            migrationBuilder.DropTable(
                name: "site_role_assignments");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_tenant_memberships_tenant_id_user_id",
                table: "tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_tenant_memberships_user_id",
                table: "tenant_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_memberships_identity",
                table: "tenant_memberships");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "tenant_memberships");

            migrationBuilder.AlterColumn<string>(
                name: "subject",
                table: "tenant_memberships",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "issuer",
                table: "tenant_memberships",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items",
                sql: "type IN ('Epic', 'Story', 'Task', 'Bug', 'Subtask')");
        }
    }
}
