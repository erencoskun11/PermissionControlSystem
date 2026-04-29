using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PermissionControlSystem.Employees.Dtos
{
    public class CreateEmployeeDto
    {
        public Guid UserId { get; set; }

        public Guid DepartmentId { get; set; }

        public string FirstName { get; set; } 

        public string LastName { get; set; }
        public string? Position { get; set; }
        public string FullName { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }
    }
}
