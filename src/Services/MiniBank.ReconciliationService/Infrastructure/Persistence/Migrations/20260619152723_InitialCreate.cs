using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniBank.ReconciliationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    network_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    matched_count = table.Column<int>(type: "integer", nullable: false),
                    unmatched_count = table.Column<int>(type: "integer", nullable: false),
                    disputed_count = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    internal_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    internal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    discrepancy_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_reconciliation_items_reconciliation_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "reconciliation_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_unprocessed",
                table: "outbox_messages",
                column: "ProcessedAt",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recon_batches_date_source",
                table: "reconciliation_batches",
                columns: new[] { "reconciliation_date", "network_source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recon_items_batch_external",
                table: "reconciliation_items",
                columns: new[] { "batch_id", "external_transaction_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "reconciliation_items");

            migrationBuilder.DropTable(
                name: "reconciliation_batches");
        }
    }
}
