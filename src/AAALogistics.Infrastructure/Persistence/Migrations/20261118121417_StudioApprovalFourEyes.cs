using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudioApprovalFourEyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "requested_by_user_id",
                table: "approval_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "required_role",
                table: "approval_requests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requested_by_user_id",
                table: "approval_requests");

            migrationBuilder.DropColumn(
                name: "required_role",
                table: "approval_requests");
        }
    }
}
