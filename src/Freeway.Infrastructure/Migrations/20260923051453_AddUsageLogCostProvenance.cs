using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageLogCostProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cost_source",
                table: "usage_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "upstream_provider",
                table: "usage_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cost_source",
                table: "usage_logs");

            migrationBuilder.DropColumn(
                name: "upstream_provider",
                table: "usage_logs");
        }
    }
}
