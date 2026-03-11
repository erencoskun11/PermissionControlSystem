using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox; // 🔥 OutboxMessage için eklendi
using System;
using System.Collections.Generic; // 🔥 List<> için eklendi
using System.Reflection;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.ObjectMapping;

namespace PermissionControlSystem.Leaves
{
   
        public class TestLeaveRequestAppService : LeaveRequestAppService
        {
            public TestLeaveRequestAppService(
                ILeaveRequestRepository leaveRequestRepository,
                INotificationService notificationService,
                IElasticSearchService elasticSearchService,
                IRepository<Employee, Guid> employeeRepository,
                IDistributedEventBus distributedEventBus,
                LeaveRequestManager leaveRequestManager,
                IDistributedCache<LeaveBalanceCacheItem, string> leaveBalanceCache,
                IDistributedCache<LeaveRequestCacheItem, string> singleLeaveCache,
                IDistributedCache<List<LeaveRequestCacheItem>, string> employeeLeavesCache,
                IRepository<OutboxMessage, Guid> outboxRepository,
                ILocalEventBus localEventBus) // 🔥 Add parameter here
                : base(
                    leaveRequestRepository,
                    notificationService,
                    elasticSearchService,
                    employeeRepository,
                    distributedEventBus,
                    leaveRequestManager,
                    leaveBalanceCache,
                    singleLeaveCache,
                    employeeLeavesCache,
                    outboxRepository,
                    localEventBus) // 🔥 Pass parameter to base here
            {
            }

            // 🔑 Kilitli (Read-only) kapıyı Reflection ile açan metodumuz
            public void SetObjectMapper(IObjectMapper mapper)
        {
            // ABP'nin ApplicationService içindeki kilitli ObjectMapper'ı buluyoruz
            var field = typeof(Volo.Abp.Application.Services.ApplicationService)
                .GetProperty("ObjectMapper", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Kilidi kırıp içeri bizim sahte (Mock) Mapper'ımızı koyuyoruz
            field?.SetValue(this, mapper);
        }
    }
}