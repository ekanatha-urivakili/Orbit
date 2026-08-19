using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemFlagCoverArchiveVotesWorklogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverAttachmentId",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "work_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "work_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "work_item_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_item_votes_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_item_worklogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes_spent = table.Column<int>(type: "integer", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_worklogs", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_item_worklogs_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_work_item_votes_tenant_item_user",
                table: "work_item_votes",
                columns: new[] { "tenant_id", "work_item_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_worklogs_tenant_id_work_item_id",
                table: "work_item_worklogs",
                columns: new[] { "tenant_id", "work_item_id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_votes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_votes FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_votes_tenant_isolation ON work_item_votes
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE work_item_worklogs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_worklogs FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_worklogs_tenant_isolation ON work_item_worklogs
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS work_item_votes_tenant_isolation ON work_item_votes;
                DROP POLICY IF EXISTS work_item_worklogs_tenant_isolation ON work_item_worklogs;
                """);

            migrationBuilder.DropTable(
                name: "work_item_votes");

            migrationBuilder.DropTable(
                name: "work_item_worklogs");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "CoverAttachmentId",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "work_items");
        }
    }
}
