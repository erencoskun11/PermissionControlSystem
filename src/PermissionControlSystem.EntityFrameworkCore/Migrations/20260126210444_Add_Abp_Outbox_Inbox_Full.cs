using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Add_Abp_Outbox_Inbox_Full : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OUTBOX için eksik kolonlar
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedTime",
                table: "AbpEventOutbox",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                table: "AbpEventOutbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AbpEventOutbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AbpEventOutbox",
                type: "uuid",
                nullable: true);


            // INBOX için eksik kolonlar
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedTime",
                table: "AbpEventInbox",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                table: "AbpEventInbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AbpEventInbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AbpEventInbox",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("ProcessedTime", "AbpEventOutbox");
            migrationBuilder.DropColumn("ProcessingStatus", "AbpEventOutbox");
            migrationBuilder.DropColumn("CorrelationId", "AbpEventOutbox");
            migrationBuilder.DropColumn("TenantId", "AbpEventOutbox");

            migrationBuilder.DropColumn("ProcessedTime", "AbpEventInbox");
            migrationBuilder.DropColumn("ProcessingStatus", "AbpEventInbox");
            migrationBuilder.DropColumn("CorrelationId", "AbpEventInbox");
            migrationBuilder.DropColumn("TenantId", "AbpEventInbox");
        }
    }
}
