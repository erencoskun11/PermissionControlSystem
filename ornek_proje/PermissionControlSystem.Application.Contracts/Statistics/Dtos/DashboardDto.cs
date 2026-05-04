using System.Collections.Generic;

namespace PermissionControlSystem.Statistics.Dtos
{
    public class DashboardDto
    {
        public StatisticsOverviewDto Overview { get; set; }
        public List<DepartmentLeaveStatDto> DepartmentStats { get; set; }
        public List<LeaveTypeStatDto> LeaveTypeStats { get; set; }
        public List<TopEmployeeStatDto> TopEmployees { get; set; }
        public List<RejectedEmployeeStatDto> MostRejected { get; set; }
        public List<MonthlyLeaveStatDto> MonthlyLeaves { get; set; }
        public List<OldestPendingLeaveStatDto> OldestPending { get; set; }
        public YearlyComparisonDto YearlyComparison { get; set; }
    }
}