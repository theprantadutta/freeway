using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageLogAvoidedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "avoided_cost_usd",
                table: "usage_logs",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avoided_cost_usd",
                table: "usage_logs");
        }
    }
}
