using PermissionControlSystem.Enums;
using PermissionControlSystem.Leaves;
using System;
using Volo.Abp.Application.Dtos;

namespace PermissionControlSystem.Leave.Dtos
{
    public class LeaveRequestDto : AuditedEntityDto<Guid>
    {

        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
        public LeaveRequestStatus Status { get; set; }
        public string? ManagerResponse { get; set; }
    }
}