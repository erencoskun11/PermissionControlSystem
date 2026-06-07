using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.ObjectMapping;

namespace PermissionControlSystem.Leaves
{
    public class TestLeaveRequestAppService : LeaveRequestAppService
    {
        // 🔥 SENIOR FIX: Constructor ismi sınıf ismiyle aynı! Sadece 9 parametre alıp base'e (ana sınıfa) paslıyor.
        public TestLeaveRequestAppService(
            ILeaveRequestRepository leaveRequestRepository,
            IElasticSearchService elasticSearchService,
            IRepository<Employee, Guid> employeeRepository,
            LeaveRequestManager leaveRequestManager,
            IDistributedCache<LeaveBalanceCacheItem, string> leaveBalanceCache,
            IDistributedCache<LeaveRequestCacheItem, string> singleLeaveCache,
            IDistributedCache<List<LeaveRequestCacheItem>, string> employeeLeavesCache,
            ILocalEventBus localEventBus)
            : base(
                leaveRequestRepository,
                elasticSearchService,
                employeeRepository,
                leaveRequestManager,
                leaveBalanceCache,
                singleLeaveCache,
                employeeLeavesCache,
                localEventBus)
        {
            // İçi boş kalacak çünkü eşlemeleri base (ana sınıf) yapıyor.
        }

        // 🔑 Kilitli (Read-only) kapıyı Reflection ile açan metodumuz
        public void SetObjectMapper(IObjectMapper mapper)
        {
            var field = typeof(Volo.Abp.Application.Services.ApplicationService)
                .GetProperty("ObjectMapper", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            field?.SetValue(this, mapper);
        }
    }
}