using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                table: "user_accounts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "user_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_app_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    digest_cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quiet_hours_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    quiet_hours_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    self_notify = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_work_item_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    enable_releases = table.Column<bool>(type: "boolean", nullable: false),
                    enable_time_tracking = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_settings", x => new { x.tenant_id, x.project_id });
                    table.ForeignKey(
                        name: "FK_project_settings_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    theme = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    density = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reduce_motion = table.Column<bool>(type: "boolean", nullable: false),
                    high_contrast = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_preferences_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    default_time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allow_member_project_creation = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_settings", x => x.tenant_id);
                    table.ForeignKey(
                        name: "FK_workspace_settings_workspaces_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE user_accounts
                SET updated_at = created_at, version = 1
                WHERE version = 0;

                INSERT INTO user_preferences
                    (user_id, locale, time_zone, theme, density, reduce_motion, high_contrast, version, updated_at)
                SELECT id, 'en-GB', 'Europe/London', 'System', 'Comfortable', FALSE, FALSE, 1, created_at
                FROM user_accounts;

                INSERT INTO notification_preferences
                    (user_id, in_app_enabled, email_enabled, digest_cadence, self_notify, version, updated_at)
                SELECT id, TRUE, TRUE, 'Daily', FALSE, 1, created_at
                FROM user_accounts;

                INSERT INTO workspace_settings
                    (tenant_id, default_locale, default_time_zone, allow_member_project_creation, version, updated_at)
                SELECT id, 'en-GB', 'Europe/London', FALSE, 1, created_at
                FROM workspaces;

                INSERT INTO project_settings
                    (tenant_id, project_id, default_work_item_type, default_priority, enable_releases, enable_time_tracking, version, updated_at)
                SELECT tenant_id, id, 'Task', 'Medium', TRUE, TRUE, 1, created_at
                FROM projects;

                ALTER TABLE workspace_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE workspace_settings FORCE ROW LEVEL SECURITY;
                CREATE POLICY workspace_settings_tenant_isolation ON workspace_settings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE project_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE project_settings FORCE ROW LEVEL SECURITY;
                CREATE POLICY project_settings_tenant_isolation ON project_settings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "project_settings");

            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropTable(
                name: "workspace_settings");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "user_accounts");
        }
    }
}
