using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freeway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiry_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "estimated"),
                    last_rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_rejection = table.Column<string>(type: "text", nullable: true),
                    warned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_notice_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_notice_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_keys_provider_fingerprint",
                table: "provider_keys",
                columns: new[] { "provider", "fingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_keys");
        }
    }
}
