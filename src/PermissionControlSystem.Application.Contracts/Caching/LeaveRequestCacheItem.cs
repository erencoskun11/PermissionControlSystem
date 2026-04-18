using System;
using PermissionControlSystem.Enums; // LeaveRequestStatus enum'ı nerede duruyorsa

namespace PermissionControlSystem.Caching
{
    public class LeaveRequestCacheItem
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }

        public LeaveRequestStatus Status { get; set; }
        public string LeaveType { get; set; } = string.Empty; 
        public string Reason { get; set; } = string.Empty;

        public LeaveRequestCacheItem() { }

        public LeaveRequestCacheItem(Guid id, Guid employeeId, string employeeFullName, DateTime startDate, DateTime endDate, int totalDays, LeaveRequestStatus status, string leaveType, string reason)
        {
            Id = id;
            EmployeeId = employeeId;
            EmployeeFullName = employeeFullName ?? string.Empty;
            StartDate = startDate;
            EndDate = endDate;
            TotalDays = totalDays;
            Status = status;
            LeaveType = leaveType ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}