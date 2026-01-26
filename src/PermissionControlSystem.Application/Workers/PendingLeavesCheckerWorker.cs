using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Leaves2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;

namespace PermissionControlSystem.Workers
{
    public class PendingLeavesCheckerWorker : AsyncPeriodicBackgroundWorkerBase
    {
        public PendingLeavesCheckerWorker(
            AbpAsyncTimer timer,
            IServiceScopeFactory serviceScopeFactory)
            :base(timer, serviceScopeFactory)
        {
            Timer.Period = 60000;

        }

        protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {
            Logger.LogInformation("Zamanlanmış görev başladı: Bekleyen izinler kontrol ediliyor...");
        
            var leaveRepository = workerContext
                .ServiceProvider
                .GetRequiredService<IRepository<LeaveRequest, Guid>>();


            var threeDaysAgo = DateTime.Now.AddDays(-3);

            var oldLeaves = await leaveRepository.GetListAsync(x =>
                x.Status == LeaveRequestStatus.Pending &&
                x.CreationTime < threeDaysAgo);

            if (oldLeaves.Count == 0)
            {
                Logger.LogInformation("---Süper! Bekleyen izin bulunamadı.");
                return;
            }

            foreach(var leave in oldLeaves)
            {
                Logger.LogWarning($"[UYARI] Dikkat! {leave.CreationTime} tarihinde oluşturulan izin (ID: {leave.Id}) hala onay bekliyor!");
            }
        }
    }
}
