using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PermissionControlSystem.Employees.Dtos
{
    public class UpdateEmployeeDto
    {
        [Required]
        public Guid DepartmentId { get; set; }

        [Required]
        [StringLength(150)]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string? Position { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }
    }
}
