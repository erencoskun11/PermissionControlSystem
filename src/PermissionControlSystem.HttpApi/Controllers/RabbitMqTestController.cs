using Microsoft.AspNetCore.Mvc;
using PermissionControlSystem.Events;
using System;
using System.Threading.Tasks;
using Volo.Abp; 
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.Controllers
{
    [Route("api/test-rabbitmq")]
    public class RabbitMqTestController : PermissionControlSystemController
    {
        private readonly IDistributedEventBus _distributedEventBus;

        public RabbitMqTestController(IDistributedEventBus distributedEventBus)
        {
            _distributedEventBus = distributedEventBus;
        }

        [HttpPost("check-connection")]
        public async Task<string> CheckRabbitMqConnection()
        {
            try
            {
                // 1. Adım: Test için sahte bir veri oluşturuyoruz
                var testEvent = new LeaveApprovedEto
                {
                    LeaveRequestId = Guid.NewGuid(),
                    ManagerResponse = "TEST MESAJI: RabbitMQ Kontrolü 🐇",
                    ApproverId = Guid.NewGuid()
                };

                // 2. Adım: RabbitMQ'ya bağlanıp mesajı fırlatmayı deniyoruz
                // Eğer RabbitMQ kapalıysa veya şifre yanlışsa burası PATLAR.
                await _distributedEventBus.PublishAsync(testEvent);

                return "✅ BAŞARILI: RabbitMQ bağlantısı sağlam! Mesaj kuyruğa iletildi.";
            }
            catch (Exception ex)
            {
                // 3. Adım: Eğer bir sorun varsa hatayı gizlemiyoruz, direkt ekrana basıyoruz.
                throw new UserFriendlyException($"❌ KRİTİK HATA: RabbitMQ'ya bağlanılamadı! \nSebep: {ex.Message}");
            }
        }
    }
}