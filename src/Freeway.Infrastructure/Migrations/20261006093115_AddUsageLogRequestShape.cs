using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageLogRequestShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reasoning_effort",
                table: "usage_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "requested_max_tokens",
                table: "usage_logs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reasoning_effort",
                table: "usage_logs");

            migrationBuilder.DropColumn(
                name: "requested_max_tokens",
                table: "usage_logs");
        }
    }
}
