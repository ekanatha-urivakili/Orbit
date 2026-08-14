using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecureInvitationOutboxPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "frontend_base_url",
                table: "outbox_email_messages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "outbox_email_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_invitation_id",
                table: "outbox_email_messages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "frontend_base_url",
                table: "outbox_email_messages");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "outbox_email_messages");

            migrationBuilder.DropColumn(
                name: "workspace_invitation_id",
                table: "outbox_email_messages");
        }
    }
}
