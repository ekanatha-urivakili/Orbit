using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    object_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    uploaded_by_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_size_bytes", "size_bytes > 0 AND size_bytes <= 26214400");
                    table.ForeignKey(
                        name: "FK_attachments_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_tenant_item_id",
                table: "attachments",
                columns: new[] { "tenant_id", "work_item_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_tenant_item_uploaded",
                table: "attachments",
                columns: new[] { "tenant_id", "work_item_id", "uploaded_at" });

            migrationBuilder.CreateIndex(
                name: "ux_attachments_object_key",
                table: "attachments",
                column: "object_key",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE attachments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE attachments FORCE ROW LEVEL SECURITY;
                CREATE POLICY attachments_tenant_isolation ON attachments
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS attachments_tenant_isolation ON attachments;");

            migrationBuilder.DropTable(
                name: "attachments");
        }
    }
}
