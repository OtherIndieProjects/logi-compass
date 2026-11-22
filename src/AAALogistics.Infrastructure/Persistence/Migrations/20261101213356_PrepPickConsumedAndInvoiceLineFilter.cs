using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrepPickConsumedAndInvoiceLineFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "consumed_quantity",
                table: "prep_pick_lines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_prep_pick_lines_consumed",
                table: "prep_pick_lines",
                sql: "consumed_quantity >= 0 AND consumed_quantity <= confirmed_quantity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prep_pick_lines_consumed",
                table: "prep_pick_lines");

            migrationBuilder.DropColumn(
                name: "consumed_quantity",
                table: "prep_pick_lines");
        }
    }
}
