using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace PermissionControlSystem.Employees.Dtos
{
    public class EmployeeDto : EntityDto<Guid>, IHasConcurrencyStamp
    {
        public Guid UserId { get; set; }
        public Guid DepartmentId { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; } 
        public string? Position { get; set; }

        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        public string? DepartmentName { get; set; }
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }
}
