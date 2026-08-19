using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemWatchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_watchers",
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
                    table.PrimaryKey("PK_work_item_watchers", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_item_watchers_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_work_item_watchers_tenant_item_user",
                table: "work_item_watchers",
                columns: new[] { "tenant_id", "work_item_id", "user_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_watchers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_watchers FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_watchers_tenant_isolation ON work_item_watchers
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS work_item_watchers_tenant_isolation ON work_item_watchers;");

            migrationBuilder.DropTable(
                name: "work_item_watchers");
        }
    }
}
