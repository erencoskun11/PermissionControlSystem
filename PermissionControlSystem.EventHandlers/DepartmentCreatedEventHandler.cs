using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.Events 
{ 
    public class DepartmentCreatedEventHandler : IDistributedEventHandler<DepartmentCreatedEto>, ITransientDependency
    {
        private readonly ILogger<DepartmentCreatedEventHandler> _logger;

        public DepartmentCreatedEventHandler(ILogger<DepartmentCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        // RabbitMQ'ya "DepartmentCreatedEto" tipinde bir mesaj düştüğü AN burası otomatik tetiklenir!
        public async Task HandleEventAsync(DepartmentCreatedEto eventData)
        {
            _logger.LogInformation("\n=======================================================");
            _logger.LogInformation($"📥 [RABBITMQ MESAJI ALINDI] Yeni Departman Eklendi!");
            _logger.LogInformation($"🏢 Departman ID: {eventData.DepartmentId}");
            _logger.LogInformation($"🏢 Departman Adı: {eventData.DepartmentName}");
            _logger.LogInformation("=======================================================\n");

            
            await Task.CompletedTask;
        }
    }
}