using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Statistics.Dtos
{
    public class DepartmentLeaveStatDto
    {
        public string DepartmentName { get; set; }
        public int TotalDays { get; set; }
        public int TotalRequests { get; set; }
    }
}
