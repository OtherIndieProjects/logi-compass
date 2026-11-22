using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SapIntegrationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intercambios_externos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    correlation_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    summary = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intercambios_externos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapeos_integracion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    external_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    internal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapped_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapeos_integracion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mensajes_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_json = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensajes_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intercambios_externos_occurred_utc",
                table: "intercambios_externos",
                column: "occurred_utc");

            migrationBuilder.CreateIndex(
                name: "ix_mapeos_integracion_system_code_entity_type_external_key",
                table: "mapeos_integracion",
                columns: new[] { "system_code", "entity_type", "external_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensajes_outbox_idempotency_key",
                table: "mensajes_outbox",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensajes_outbox_status_next_attempt_utc",
                table: "mensajes_outbox",
                columns: new[] { "status", "next_attempt_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intercambios_externos");

            migrationBuilder.DropTable(
                name: "mapeos_integracion");

            migrationBuilder.DropTable(
                name: "mensajes_outbox");
        }
    }
}
