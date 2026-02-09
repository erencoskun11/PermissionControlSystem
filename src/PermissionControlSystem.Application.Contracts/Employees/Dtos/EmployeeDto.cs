using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Application.Dtos;

namespace PermissionControlSystem.Employees.Dtos
{
    public class EmployeeDto : EntityDto<Guid>
    {
        public Guid UserId { get; set; }
        public Guid DepartmentId { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Position { get; set; }

        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        public string? DepartmentName { get; set; }
    }
}
