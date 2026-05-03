using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Domain.Entities;

namespace PermissionControlSystem.Leaves.Dtos
{
    public class ApproveLeaveDto : IHasConcurrencyStamp
    {
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }
}
