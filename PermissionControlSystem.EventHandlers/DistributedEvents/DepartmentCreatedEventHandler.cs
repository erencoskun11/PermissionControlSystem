using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents 
{ 
    public class DepartmentCreatedEventHandler : IDistributedEventHandler<DepartmentCreatedEto>, ITransientDependency
    {
        private readonly ILogger<DepartmentCreatedEventHandler> _logger;
        private readonly IRepository<IncomingMessage, Guid> _incomingMessageRepository;

        public DepartmentCreatedEventHandler(
            ILogger<DepartmentCreatedEventHandler> logger,
            IRepository<IncomingMessage, Guid> incomingMessageRepository)
        {
            _logger = logger;
            _incomingMessageRepository = incomingMessageRepository;
        }

        // RabbitMQ'ya "DepartmentCreatedEto" tipinde bir mesaj düştüğü AN burası otomatik tetiklenir!
        public async Task HandleEventAsync(DepartmentCreatedEto eventData)
        {
            if (await _incomingMessageRepository.AnyAsync(x => x.EventId == eventData.EventId))
            {
                _logger.LogWarning($"[INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). İşlem atlanıyor.");
                return;
            }

            _logger.LogInformation("\n=======================================================");
            _logger.LogInformation($"📥 [RABBITMQ MESAJI ALINDI] Yeni Departman Eklendi!");
            _logger.LogInformation($"🏢 Departman ID: {eventData.DepartmentId}");
            _logger.LogInformation($"🏢 Departman Adı: {eventData.DepartmentName}");
            _logger.LogInformation("=======================================================\n");

            await _incomingMessageRepository.InsertAsync(
                new IncomingMessage(eventData.EventId, "DepartmentCreated"),
                autoSave: true
            );
        }
    }
}