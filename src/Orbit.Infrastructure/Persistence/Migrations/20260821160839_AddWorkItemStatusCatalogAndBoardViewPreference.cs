using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStatusCatalogAndBoardViewPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_project_id_status_rank",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_status",
                table: "work_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_board_columns",
                table: "board_columns");

            migrationBuilder.CreateTable(
                name: "work_item_status_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    color_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_status_definitions", x => x.id);
                    table.UniqueConstraint("AK_work_item_status_definitions_tenant_id_project_id_id", x => new { x.tenant_id, x.project_id, x.id });
                    table.CheckConstraint("ck_work_item_status_definitions_order", "\"order\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("ck_work_item_status_definitions_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_work_item_status_definitions_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "board_view_preferences",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hide_done_items_after = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    column_size_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hidden_fields = table.Column<string[]>(type: "text[]", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board_view_preferences", x => new { x.tenant_id, x.user_id, x.project_id });
                    table.ForeignKey(
                        name: "FK_board_view_preferences_projects_tenant_id_project_id",
                        columns: x => new { x.tenant_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_board_view_preferences_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // status_id starts nullable so the backfill below can populate it from the old `status`
            // enum column before either column is dropped; both are tightened to NOT NULL afterwards.
            migrationBuilder.AddColumn<Guid>(
                name: "status_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "status_id",
                table: "board_columns",
                type: "uuid",
                nullable: true);

            // Seed the six default statuses for every existing project - the same set
            // WorkItemStatusDefinition.CreateSoftwareDefaults seeds for a newly created project - so
            // existing data has somewhere to point before the old `status` column is dropped.
            migrationBuilder.Sql("""
                INSERT INTO work_item_status_definitions
                    (id, tenant_id, project_id, key, name, category, "order", color_token, is_system, version, created_at, updated_at)
                SELECT gen_random_uuid(), p.tenant_id, p.id, seed.key, seed.name, seed.category, seed."order", seed.color_token, true, 1, now(), now()
                FROM projects p
                CROSS JOIN (VALUES
                    ('backlog', 'Backlog', 'ToDo', 10, 'slate'),
                    ('selected', 'Selected', 'ToDo', 20, 'cyan'),
                    ('in-progress', 'In progress', 'InProgress', 30, 'blue'),
                    ('in-review', 'In review', 'InProgress', 40, 'amber'),
                    ('done', 'Done', 'Done', 50, 'green'),
                    ('blocked', 'Blocked', 'InProgress', 60, 'red')
                ) AS seed(key, name, category, "order", color_token);
                """);

            migrationBuilder.Sql("""
                UPDATE work_items wi
                SET status_id = wsd.id
                FROM work_item_status_definitions wsd
                WHERE wsd.tenant_id = wi.tenant_id
                  AND wsd.project_id = wi.project_id
                  AND wsd.key = CASE wi.status
                      WHEN 'Backlog' THEN 'backlog'
                      WHEN 'Selected' THEN 'selected'
                      WHEN 'InProgress' THEN 'in-progress'
                      WHEN 'InReview' THEN 'in-review'
                      WHEN 'Done' THEN 'done'
                      WHEN 'Blocked' THEN 'blocked'
                  END;
                """);

            migrationBuilder.Sql("""
                UPDATE board_columns bc
                SET status_id = wsd.id
                FROM work_item_status_definitions wsd
                WHERE wsd.tenant_id = bc.tenant_id
                  AND wsd.project_id = bc.project_id
                  AND wsd.key = CASE bc.status
                      WHEN 'Backlog' THEN 'backlog'
                      WHEN 'Selected' THEN 'selected'
                      WHEN 'InProgress' THEN 'in-progress'
                      WHEN 'InReview' THEN 'in-review'
                      WHEN 'Done' THEN 'done'
                      WHEN 'Blocked' THEN 'blocked'
                  END;
                """);

            migrationBuilder.DropColumn(
                name: "status",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "status",
                table: "board_columns");

            migrationBuilder.AlterColumn<Guid>(
                name: "status_id",
                table: "work_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "status_id",
                table: "board_columns",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_board_columns",
                table: "board_columns",
                columns: new[] { "tenant_id", "project_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_project_id_status_id_rank",
                table: "work_items",
                columns: new[] { "tenant_id", "project_id", "status_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "IX_board_view_preferences_tenant_id_project_id",
                table: "board_view_preferences",
                columns: new[] { "tenant_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "IX_board_view_preferences_user_id",
                table: "board_view_preferences",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_status_definitions_tenant_id_project_id_key",
                table: "work_item_status_definitions",
                columns: new[] { "tenant_id", "project_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_status_definitions_tenant_id_project_id_order",
                table: "work_item_status_definitions",
                columns: new[] { "tenant_id", "project_id", "order" });

            migrationBuilder.AddForeignKey(
                name: "FK_board_columns_work_item_status_definitions_tenant_id_projec~",
                table: "board_columns",
                columns: new[] { "tenant_id", "project_id", "status_id" },
                principalTable: "work_item_status_definitions",
                principalColumns: new[] { "tenant_id", "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_work_item_status_definitions_tenant_id_project_i~",
                table: "work_items",
                columns: new[] { "tenant_id", "project_id", "status_id" },
                principalTable: "work_item_status_definitions",
                principalColumns: new[] { "tenant_id", "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_status_definitions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_status_definitions FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_status_definitions_tenant_isolation ON work_item_status_definitions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE board_view_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE board_view_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY board_view_preferences_tenant_isolation ON board_view_preferences
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS work_item_status_definitions_tenant_isolation ON work_item_status_definitions;");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS board_view_preferences_tenant_isolation ON board_view_preferences;");

            migrationBuilder.DropForeignKey(
                name: "FK_board_columns_work_item_status_definitions_tenant_id_projec~",
                table: "board_columns");

            migrationBuilder.DropForeignKey(
                name: "FK_work_items_work_item_status_definitions_tenant_id_project_i~",
                table: "work_items");

            migrationBuilder.DropTable(
                name: "board_view_preferences");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "work_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "board_columns",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE work_items wi
                SET status = CASE wsd.key
                    WHEN 'backlog' THEN 'Backlog'
                    WHEN 'selected' THEN 'Selected'
                    WHEN 'in-progress' THEN 'InProgress'
                    WHEN 'in-review' THEN 'InReview'
                    WHEN 'done' THEN 'Done'
                    WHEN 'blocked' THEN 'Blocked'
                    ELSE 'Backlog'
                END
                FROM work_item_status_definitions wsd
                WHERE wsd.id = wi.status_id;
                """);

            migrationBuilder.Sql("""
                UPDATE board_columns bc
                SET status = CASE wsd.key
                    WHEN 'backlog' THEN 'Backlog'
                    WHEN 'selected' THEN 'Selected'
                    WHEN 'in-progress' THEN 'InProgress'
                    WHEN 'in-review' THEN 'InReview'
                    WHEN 'done' THEN 'Done'
                    WHEN 'blocked' THEN 'Blocked'
                    ELSE 'Backlog'
                END
                FROM work_item_status_definitions wsd
                WHERE wsd.id = bc.status_id;
                """);

            migrationBuilder.DropTable(
                name: "work_item_status_definitions");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_project_id_status_id_rank",
                table: "work_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_board_columns",
                table: "board_columns");

            migrationBuilder.DropColumn(
                name: "status_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "status_id",
                table: "board_columns");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "work_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "board_columns",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldNullable: true,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_board_columns",
                table: "board_columns",
                columns: new[] { "tenant_id", "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_project_id_status_rank",
                table: "work_items",
                columns: new[] { "tenant_id", "project_id", "status", "rank" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_status",
                table: "work_items",
                sql: "status IN ('Backlog', 'Selected', 'InProgress', 'InReview', 'Done', 'Blocked')");
        }
    }
}
