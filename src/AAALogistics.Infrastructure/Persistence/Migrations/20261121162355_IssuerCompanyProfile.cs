using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IssuerCompanyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "issuer_fiscal_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "issuer_fiscal_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iban",
                table: "issuer_fiscal_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "irpf_percent",
                table: "issuer_fiscal_profiles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "legal_footer_text",
                table: "issuer_fiscal_profiles",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_png_relative_path",
                table: "issuer_fiscal_profiles",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_due_days",
                table: "issuer_fiscal_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "issuer_fiscal_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "issuer_fiscal_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_png_relative_path",
                table: "issuer_fiscal_profiles",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signer_name",
                table: "issuer_fiscal_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signer_title",
                table: "issuer_fiscal_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "issuer_fiscal_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_name",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "email",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "iban",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "irpf_percent",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "legal_footer_text",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "logo_png_relative_path",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "payment_due_days",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "signature_png_relative_path",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "signer_name",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "signer_title",
                table: "issuer_fiscal_profiles");

            migrationBuilder.DropColumn(
                name: "website",
                table: "issuer_fiscal_profiles");
        }
    }
}
