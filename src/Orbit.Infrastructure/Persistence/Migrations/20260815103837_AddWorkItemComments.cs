using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    mentioned_user_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_comments", x => x.id);
                    table.CheckConstraint("ck_work_item_comments_body_length", "char_length(body) BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_work_item_comments_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_work_item_comments_work_items_tenant_id_work_item_id",
                        columns: x => new { x.tenant_id, x.work_item_id },
                        principalTable: "work_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_tenant_item_created",
                table: "work_item_comments",
                columns: new[] { "tenant_id", "work_item_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_tenant_item_id",
                table: "work_item_comments",
                columns: new[] { "tenant_id", "work_item_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_item_comments");
        }
    }
}
