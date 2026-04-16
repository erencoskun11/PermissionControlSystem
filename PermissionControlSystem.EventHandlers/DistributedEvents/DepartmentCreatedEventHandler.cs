using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents 
{ 
    public class DepartmentCreatedEventHandler : IDistributedEventHandler<DepartmentCreatedEto>, ITransientDependency
    {
        private readonly ILogger<DepartmentCreatedEventHandler> _logger;
        private readonly IDistributedCache<string, string> _inboxCache;
        public DepartmentCreatedEventHandler(
            ILogger<DepartmentCreatedEventHandler> logger,
            IDistributedCache<string,string> inboxCache)
        {
            _logger = logger;
            _inboxCache = inboxCache;

        }

        // RabbitMQ'ya "DepartmentCreatedEto" tipinde bir mesaj düştüğü AN burası otomatik tetiklenir!
        public async Task HandleEventAsync(DepartmentCreatedEto eventData)
        {
            // 🔥 1. INBOX KONTROLÜ: Redis'e soruyoruz, bu EventId daha önce geldi mi?
            var inboxKey = $"Inbox_DeptCreated_{eventData.EventId}";
            var alreadyProcessed = await _inboxCache.GetAsync(inboxKey);
            
            if (alreadyProcessed != null)
            {
                _logger.LogWarning($"🛑 [INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). Mükerrer işlem engellendi.");
                return;
            }

            // 🔥 2. ASIL İŞLEM
            _logger.LogInformation("\n=======================================================");
            _logger.LogInformation($"📥 [RABBITMQ MESAJI ALINDI] Yeni Departman Eklendi!");
            _logger.LogInformation($"🏢 Departman ID: {eventData.DepartmentId}");
            _logger.LogInformation($"🏢 Departman Adı: {eventData.DepartmentName}");
            _logger.LogInformation("=======================================================\n");

            // 🔥 3. KAPIYI KİLİTLE: İşlem bittiyse mührü Redis'e bas (7 gün boyunca aynı mesaj gelirse reddet)
            await _inboxCache.SetAsync(inboxKey, "Processed", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            });
        }
    }
}