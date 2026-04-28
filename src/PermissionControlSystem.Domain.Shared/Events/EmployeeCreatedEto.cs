using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Events
{
    public class EmployeeCreatedEto
    {
        public Guid EventId { get; set; } = Guid.NewGuid(); 
        public Guid EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
    }
}
