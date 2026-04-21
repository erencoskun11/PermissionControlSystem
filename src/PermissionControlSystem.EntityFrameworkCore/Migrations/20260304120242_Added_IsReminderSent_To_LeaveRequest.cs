using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Added_IsReminderSent_To_LeaveRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReminderSent",
                table: "AppLeaveRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReminderSent",
                table: "AppLeaveRequests");
        }
    }
}
