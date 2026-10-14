using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageLogStreaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "streamed",
                table: "usage_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "time_to_first_token_ms",
                table: "usage_logs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "streamed",
                table: "usage_logs");

            migrationBuilder.DropColumn(
                name: "time_to_first_token_ms",
                table: "usage_logs");
        }
    }
}
