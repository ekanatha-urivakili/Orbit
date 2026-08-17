using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestructureWorkItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_items_work_items_tenant_id_linked_work_item_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_linked_work_item_id",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_link",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_link_type",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "link_type",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "linked_work_item_id",
                table: "work_items");

            migrationBuilder.CreateTable(
                name: "work_item_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_links", x => x.id);
                    table.CheckConstraint("ck_work_item_links_distinct", "source_work_item_id <> target_work_item_id");
                    table.CheckConstraint("ck_work_item_links_kind", "kind IN ('Blocks', 'RelatesTo', 'Duplicates')");
                    table.ForeignKey(
                        name: "FK_work_item_links_work_items_tenant_id_source_work_item_id",
                        columns: x => new { x.tenant_id, x.source_work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_item_links_work_items_tenant_id_target_work_item_id",
                        columns: x => new { x.tenant_id, x.target_work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_links_tenant_id_source_work_item_id",
                table: "work_item_links",
                columns: new[] { "tenant_id", "source_work_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_links_tenant_id_source_work_item_id_target_work_i~",
                table: "work_item_links",
                columns: new[] { "tenant_id", "source_work_item_id", "target_work_item_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_links_tenant_id_target_work_item_id",
                table: "work_item_links",
                columns: new[] { "tenant_id", "target_work_item_id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE work_item_links ENABLE ROW LEVEL SECURITY;
                ALTER TABLE work_item_links FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_item_links_tenant_isolation ON work_item_links
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS work_item_links_tenant_isolation ON work_item_links;");

            migrationBuilder.DropTable(
                name: "work_item_links");

            migrationBuilder.AddColumn<string>(
                name: "link_type",
                table: "work_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "linked_work_item_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_linked_work_item_id",
                table: "work_items",
                columns: new[] { "tenant_id", "linked_work_item_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_link",
                table: "work_items",
                sql: "(link_type IS NULL) = (linked_work_item_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_link_type",
                table: "work_items",
                sql: "link_type IS NULL OR link_type IN ('DependsOn', 'Blocks', 'RelatesTo')");

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_work_items_tenant_id_linked_work_item_id",
                table: "work_items",
                columns: new[] { "tenant_id", "linked_work_item_id" },
                principalTable: "work_items",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
