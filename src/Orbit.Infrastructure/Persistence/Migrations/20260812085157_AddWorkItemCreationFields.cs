using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemCreationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items");

            migrationBuilder.AddColumn<string>(
                name: "acceptance_criteria",
                table: "work_items",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assignee_user_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "attachment_names",
                table: "work_items",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "countries",
                table: "work_items",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "developer_user_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "epic_name",
                table: "work_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identified_on",
                table: "work_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "labels",
                table: "work_items",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

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

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "product_owner_user_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sprint_name",
                table: "work_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "steps_to_conduct",
                table: "work_items",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "story_points",
                table: "work_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_work_items_tenant_id_id",
                table: "work_items",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_linked_work_item_id",
                table: "work_items",
                columns: new[] { "tenant_id", "linked_work_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_parent_id",
                table: "work_items",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_epic_name",
                table: "work_items",
                sql: "type <> 'Epic' OR epic_name IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_link",
                table: "work_items",
                sql: "(link_type IS NULL) = (linked_work_item_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_link_type",
                table: "work_items",
                sql: "link_type IS NULL OR link_type IN ('DependsOn', 'Blocks', 'RelatesTo')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_story_points",
                table: "work_items",
                sql: "story_points IS NULL OR story_points BETWEEN 0 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items",
                sql: "type IN ('Initiative', 'Epic', 'Task', 'Story', 'Spike', 'Test', 'Feature', 'Request', 'Bug', 'Subtask')");

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_work_items_tenant_id_linked_work_item_id",
                table: "work_items",
                columns: new[] { "tenant_id", "linked_work_item_id" },
                principalTable: "work_items",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_work_items_tenant_id_parent_id",
                table: "work_items",
                columns: new[] { "tenant_id", "parent_id" },
                principalTable: "work_items",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_items_work_items_tenant_id_linked_work_item_id",
                table: "work_items");

            migrationBuilder.DropForeignKey(
                name: "FK_work_items_work_items_tenant_id_parent_id",
                table: "work_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_work_items_tenant_id_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_linked_work_item_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_parent_id",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_epic_name",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_link",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_link_type",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_story_points",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "acceptance_criteria",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "assignee_user_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "attachment_names",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "countries",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "developer_user_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "epic_name",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "identified_on",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "labels",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "link_type",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "linked_work_item_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "product_owner_user_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "sprint_name",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "steps_to_conduct",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "story_points",
                table: "work_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_type",
                table: "work_items",
                sql: "type IN ('Epic', 'Task', 'Story', 'Feature', 'Request', 'Bug', 'Subtask')");
        }
    }
}
