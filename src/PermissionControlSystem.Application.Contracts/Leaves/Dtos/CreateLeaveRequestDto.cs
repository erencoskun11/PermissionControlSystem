using PermissionControlSystem.Leaves;
using System;
using System.ComponentModel.DataAnnotations;

namespace PermissionControlSystem.Leave.Dtos
{
    public class CreateLeaveRequestDto
    {
        public Guid EmployeeId { get; set; }

        public LeaveType LeaveType { get; set; } 
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string Reason { get; set; }
    }
}