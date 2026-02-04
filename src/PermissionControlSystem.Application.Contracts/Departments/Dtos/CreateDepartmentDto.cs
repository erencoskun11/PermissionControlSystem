using System.ComponentModel.DataAnnotations;

namespace PermissionControlSystem.Departments.Dtos
{
    public class CreateDepartmentDto
    {
        [Required(ErrorMessage = "Departman adı boş bırakılamaz!")]
        [StringLength(128)] 
        public string Name { get; set; }
        public string Description { get; set; }
    }
}