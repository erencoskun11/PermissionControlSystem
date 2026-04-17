using Microsoft.Extensions.Logging;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Events.Department;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;

namespace PermissionControlSystem.EventHandlers.LocalEvents.Departments
{
    // 🔥 İşte AppService'in attığı o 4 yükü sırtlayan gerçek kahraman!
    public class DepartmentCreatedLocalHandler : ILocalEventHandler<DepartmentCreatedEvent>, ITransientDependency
    {
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
        private readonly INotificationService _notificationService;
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly IDistributedCache<List<DepartmentCacheItem>, string> _departmentCache;
        private readonly ILogger<DepartmentCreatedLocalHandler> _logger;
        private readonly IGuidGenerator _guidGenerator;

        public DepartmentCreatedLocalHandler(
            IRepository<OutboxMessage, Guid> outboxRepository,
            INotificationService notificationService,
            IDistributedEventBus distributedEventBus,
            IDistributedCache<List<DepartmentCacheItem>, string> departmentCache,
            ILogger<DepartmentCreatedLocalHandler> logger,
            IGuidGenerator guidGenerator)
        {
            _outboxRepository = outboxRepository;
            _notificationService = notificationService;
            _distributedEventBus = distributedEventBus;
            _departmentCache = departmentCache;
            _logger = logger;
            _guidGenerator = guidGenerator;
        }

        public async Task HandleEventAsync(DepartmentCreatedEvent eventData)
        {
            _logger.LogInformation($"[HANDLER] Yeni departman eklendi duyuldu: {eventData.DepartmentName}");

            // 1. OUTBOX GÖREVİ
            var outboxMessage = new OutboxMessage(
                _guidGenerator.Create(),
                "Department",
                JsonSerializer.Serialize(new
                {
                    Action = "Created",
                    Id = eventData.DepartmentId,
                    Name = eventData.DepartmentName,
                    Description = eventData.Description
                })
            );
            await _outboxRepository.InsertAsync(outboxMessage);

            // 2. BİLDİRİM GÖREVİ
            await _notificationService.AddNotificationAsync($"🏢 Yeni Departman: '{eventData.DepartmentName}' adlı departman sisteme eklendi.");

            // 3. RABBITMQ GÖREVİ (Distributed Event)
            await _distributedEventBus.PublishAsync(new DepartmentCreatedEto
            {
                DepartmentId = eventData.DepartmentId,
                DepartmentName = eventData.DepartmentName,
                Message = "Yeni departman sisteme eklendi!"
            });

            // 4. CACHE TEMİZLEME GÖREVİ
            await _departmentCache.RemoveAsync("AllActiveDepartments");

            _logger.LogInformation($"[HANDLER] Departman için tüm yan etkiler (Cache, Bildirim, Outbox) başarıyla tamamlandı!");
        }
    }
}