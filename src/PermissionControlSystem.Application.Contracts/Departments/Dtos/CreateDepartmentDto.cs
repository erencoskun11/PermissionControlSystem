using System.ComponentModel.DataAnnotations;

namespace PermissionControlSystem.Departments.Dtos
{
    public class CreateDepartmentDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}