using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddedUniqueIndexToDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppDepartments_Name",
                table: "AppDepartments",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDepartments_Name",
                table: "AppDepartments");
        }
    }
}
