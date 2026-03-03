using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PermissionControlSystem.Departments.Dtos
{
    public class UpdateDepartmentDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
