using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PermissionControlSystem.Departments.Dtos
{
    public class UpdateDepartmentDto
    {
        [Required(ErrorMessage = "Departman adı boş bırakılamaz!")]
        [StringLength(128)]
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
