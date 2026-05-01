using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Volo.Abp.Domain.Entities;

namespace PermissionControlSystem.Departments.Dtos
{
    public class UpdateDepartmentDto : IHasConcurrencyStamp
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }
}
