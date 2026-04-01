using Microsoft.AspNetCore.Identity.UI.Services;
using PermissionControlSystem.Events.LeaveRequest;
using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace PermissionControlSystem.EventHandlers.LocalEvents.Leaves
{
    public class LeaveReminderNeededLocalHandler :
        ILocalEventHandler<LeaveReminderNeededEvent>,
        ITransientDependency
    {
        private readonly Volo.Abp.EventBus.Distributed.IDistributedEventBus _distributedEventBus;

        public LeaveReminderNeededLocalHandler(Volo.Abp.EventBus.Distributed.IDistributedEventBus distributedEventBus)
        {
            _distributedEventBus = distributedEventBus;
        }

        public async Task HandleEventAsync(LeaveReminderNeededEvent eventData)
        {
            await _distributedEventBus.PublishAsync(new PermissionControlSystem.Events.LeaveReminderNeededEto
            {
                LeaveRequestId = eventData.LeaveRequestId,
                EmployeeId = eventData.EmployeeId,
                CreationTime = eventData.CreationTime
            });
        }
    }
}
