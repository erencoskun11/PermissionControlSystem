using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Add_UniqueIndex_IncomingMessage_EventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppIncomingMessages_EventId",
                table: "AppIncomingMessages");

            migrationBuilder.AlterColumn<string>(
                name: "ManagerResponse",
                table: "AppLeaveRequests",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_AppIncomingMessages_EventId",
                table: "AppIncomingMessages",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppIncomingMessages_EventId",
                table: "AppIncomingMessages");

            migrationBuilder.AlterColumn<string>(
                name: "ManagerResponse",
                table: "AppLeaveRequests",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppIncomingMessages_EventId",
                table: "AppIncomingMessages",
                column: "EventId");
        }
    }
}
