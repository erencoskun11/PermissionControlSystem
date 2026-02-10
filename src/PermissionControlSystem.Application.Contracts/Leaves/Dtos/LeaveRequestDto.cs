using PermissionControlSystem.Enums;
using System;
using Volo.Abp.Application.Dtos;

namespace PermissionControlSystem.Leave.Dtos
{
    public class LeaveRequestDto : AuditedEntityDto<Guid>
    {
        // public Guid LeaveTypeId { get; set; } <--- BU SATIRI SİL! (Hata Kaynağı)

        // StaffId ekleyelim ki kimin izni olduğunu görelim
        public Guid EmployeeId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
        public LeaveRequestStatus Status { get; set; }
        public string ManagerResponse { get; set; }
    }
}