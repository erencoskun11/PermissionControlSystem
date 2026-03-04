using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Outbox;
using PermissionControlSystem.Services;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace PermissionControlSystem.Workers
{
    public class OutboxWorker : AsyncPeriodicBackgroundWorkerBase
    {
        public OutboxWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
            : base(timer, serviceScopeFactory)
        {
            Timer.Period = 10000; // 10 saniyede bir kontrol et
        }

        
    }
}