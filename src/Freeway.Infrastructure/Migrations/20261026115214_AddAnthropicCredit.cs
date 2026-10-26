using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnthropicCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_cost_usd",
                table: "usage_logs",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount_usd = table.Column<decimal>(type: "numeric(20,10)", precision: 20, scale: 10, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "auto"),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credit_grants_provider_starts_at",
                table: "credit_grants",
                columns: new[] { "provider", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_grants");

            migrationBuilder.DropColumn(
                name: "credit_cost_usd",
                table: "usage_logs");
        }
    }
}
