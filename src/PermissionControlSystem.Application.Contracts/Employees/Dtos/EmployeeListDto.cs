using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Application.Dtos;

namespace PermissionControlSystem.Employees.Dtos
{
    public class EmployeeListDto : EntityDto<Guid>
    {
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Position { get; set; }
        public Guid DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }
}
