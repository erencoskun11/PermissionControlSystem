using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Statistics.Dtos
{
    public class YearlyComparisonDto
    {
        public int PreviousYearTotal { get; set; }
        public int CurrentYearTotal { get; set; }
        public double ChangeRate { get; set; }
    }
}
