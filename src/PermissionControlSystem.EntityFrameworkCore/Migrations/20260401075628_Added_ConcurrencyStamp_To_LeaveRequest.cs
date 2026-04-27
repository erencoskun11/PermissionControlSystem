using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Added_ConcurrencyStamp_To_LeaveRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "AppLeaveRequests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "AppLeaveRequests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "AppLeaveRequests");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "AppLeaveRequests");
        }
    }
}
