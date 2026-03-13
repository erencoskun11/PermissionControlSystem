using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Employee_Relation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppDepartments_DepartmentId1",
                table: "AppEmployees");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId1",
                table: "AppEmployees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppDepartments_DepartmentId1",
                table: "AppEmployees",
                column: "DepartmentId1",
                principalTable: "AppDepartments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppDepartments_DepartmentId1",
                table: "AppEmployees");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId1",
                table: "AppEmployees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppDepartments_DepartmentId1",
                table: "AppEmployees",
                column: "DepartmentId1",
                principalTable: "AppDepartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
