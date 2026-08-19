using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStartDateAndTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "start_date",
                table: "work_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_items_tenant_id_team_id",
                table: "work_items",
                columns: new[] { "tenant_id", "team_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_work_items_teams_tenant_id_team_id",
                table: "work_items",
                columns: new[] { "tenant_id", "team_id" },
                principalTable: "teams",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_items_teams_tenant_id_team_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "IX_work_items_tenant_id_team_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "work_items");
        }
    }
}
