using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Relationships_V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppLeaveRequests_AppEmployees_EmployeeId1",
                table: "AppLeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_AppLeaveRequests_EmployeeId1",
                table: "AppLeaveRequests");

            migrationBuilder.DropColumn(
                name: "EmployeeId1",
                table: "AppLeaveRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId1",
                table: "AppLeaveRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLeaveRequests_EmployeeId1",
                table: "AppLeaveRequests",
                column: "EmployeeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AppLeaveRequests_AppEmployees_EmployeeId1",
                table: "AppLeaveRequests",
                column: "EmployeeId1",
                principalTable: "AppEmployees",
                principalColumn: "Id");
        }
    }
}
