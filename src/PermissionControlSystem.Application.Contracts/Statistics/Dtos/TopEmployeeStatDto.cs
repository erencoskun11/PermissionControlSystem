using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Statistics.Dtos
{
    public class TopEmployeeStatDto
    {
        public string EmployeeName { get; set; }
        public string DepartmentName { get; set; }
        public int RequestCount { get; set; }
        public int TotalDays { get; set; }
    }
}
