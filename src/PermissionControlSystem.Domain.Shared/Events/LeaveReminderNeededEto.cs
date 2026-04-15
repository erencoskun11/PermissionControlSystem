using System;
using Volo.Abp.EventBus;

namespace PermissionControlSystem.Events;

[Serializable]
[EventName("leave.reminder.needed")]
public class LeaveReminderNeededEto
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime CreationTime { get; set; }
}