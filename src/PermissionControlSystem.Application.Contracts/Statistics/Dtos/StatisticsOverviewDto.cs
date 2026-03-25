using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Statistics.Dtos
{
    public class StatisticsOverviewDto
    {
        public int TotalRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int TotalLeaveDays { get; set; }
    }
}
