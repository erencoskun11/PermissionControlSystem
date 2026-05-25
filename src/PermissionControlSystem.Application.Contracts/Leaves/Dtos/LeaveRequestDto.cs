using PermissionControlSystem.Enums;
using PermissionControlSystem.Leaves;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace PermissionControlSystem.Leave.Dtos
{
    public class LeaveRequestDto : AuditedEntityDto<Guid>,IHasConcurrencyStamp
    {

        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
        public LeaveRequestStatus Status { get; set; }
        public string? ManagerResponse { get; set; }

        // 🔥 MÜHÜR BURADA EKRANA GİDİYOR
        public string ConcurrencyStamp { get; set; }
    }
}