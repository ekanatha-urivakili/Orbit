using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemHistoryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_history_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    old_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_history_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_item_history_entries_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_history_entries_tenant_item_changed",
                table: "work_item_history_entries",
                columns: new[] { "tenant_id", "work_item_id", "changed_at" });

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_history_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_history_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_history_entries_tenant_isolation ON work_item_history_entries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS work_item_history_entries_tenant_isolation ON work_item_history_entries;
                """);

            migrationBuilder.DropTable(
                name: "work_item_history_entries");
        }
    }
}
