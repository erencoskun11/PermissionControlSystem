using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.EventBus;

namespace PermissionControlSystem.Events
{
    [EventName("PermissionControlSystem.Department.Created")]
    public class DepartmentCreatedEto
    {
        public Guid EventId { get; set; } = Guid.NewGuid(); // 🔥 BÖYLECE MESAJIN KİMLİĞİ OLACAK
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Message { get; set; }
    }
}
