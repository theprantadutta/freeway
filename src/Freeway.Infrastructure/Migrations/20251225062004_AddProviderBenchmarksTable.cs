using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderBenchmarksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_benchmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    error_code = table.Column<int>(type: "integer", nullable: true),
                    tested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_benchmarks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_benchmarks_provider",
                table: "provider_benchmarks",
                column: "provider_name");

            migrationBuilder.CreateIndex(
                name: "ix_provider_benchmarks_tested_at",
                table: "provider_benchmarks",
                column: "tested_at");

            migrationBuilder.CreateIndex(
                name: "ix_provider_benchmarks_provider_tested_at",
                table: "provider_benchmarks",
                columns: new[] { "provider_name", "tested_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_benchmarks");
        }
    }
}
