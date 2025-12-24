using System;
using System.Collections.Generic;
using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    api_key_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    rate_limit_per_minute = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    model_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    output_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(20,10)", precision: 20, scale: 10, nullable: false, defaultValue: 0m),
                    prompt_cost_per_token = table.Column<decimal>(type: "numeric(20,15)", precision: 20, scale: 15, nullable: true),
                    completion_cost_per_token = table.Column<decimal>(type: "numeric(20,15)", precision: 20, scale: 15, nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    request_messages = table.Column<List<ChatMessage>>(type: "jsonb", nullable: true),
                    response_content = table.Column<string>(type: "text", nullable: true),
                    finish_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    request_params = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_usage_logs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projects_api_key_hash",
                table: "projects",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_is_active",
                table: "projects",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_created_at",
                table: "usage_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_project_id",
                table: "usage_logs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_project_id_created_at",
                table: "usage_logs",
                columns: new[] { "project_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usage_logs");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
