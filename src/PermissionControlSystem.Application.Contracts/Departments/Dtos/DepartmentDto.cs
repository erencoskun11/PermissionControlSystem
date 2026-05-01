using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
namespace PermissionControlSystem.Departments.Dtos
{
     public class DepartmentDto : EntityDto<Guid>, IHasConcurrencyStamp 
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int EmployeeCount { get; set; }
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }
}
