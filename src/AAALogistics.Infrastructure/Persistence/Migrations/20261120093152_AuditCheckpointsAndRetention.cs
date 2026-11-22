using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditCheckpointsAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    through_sequence = table.Column<long>(type: "bigint", nullable: false),
                    through_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_checkpoints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_checkpoints_created_utc",
                table: "audit_checkpoints",
                column: "created_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_checkpoints_through_sequence",
                table: "audit_checkpoints",
                column: "through_sequence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_checkpoints");
        }
    }
}
