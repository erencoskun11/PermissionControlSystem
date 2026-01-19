using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace PermissionControlSystem.Leaves2
{
    public class LeaveType : FullAuditedAggregateRoot<Guid>
    {
        public string Name { get; set; } // Örn: Yıllık İzin
        public int DefaultDays { get; set; } // Varsayılan gün sayısı

        protected LeaveType() { }

        public LeaveType(Guid id, string name, int defaultDays) : base(id)
        {
            Name = name;
            DefaultDays = defaultDays;
        }
    }
}