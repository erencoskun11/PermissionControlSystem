using System;
using System.Threading.Tasks;
using PermissionControlSystem.Events;
using Shouldly;
using Volo.Abp.EventBus.Distributed;
using Xunit;
namespace PermissionControlSystem;

[Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
public class RabbitMqIntegrationTest : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
{
    private readonly IDistributedEventBus _distributedEventBus;

    public RabbitMqIntegrationTest()
    {
        _distributedEventBus = GetRequiredService<IDistributedEventBus>();
    }

    [Fact]
    public async Task Should_Connect_And_Publish_To_RabbitMq()
    {
        
        var testEvent = new LeaveApprovedEto
        {
            LeaveRequestId = Guid.NewGuid(),
            ManagerResponse = "TEST OTOMASYONU: RabbitMQ Bağlantı Kontrolü 🐇",
            ApproverId = Guid.NewGuid()
        };

        // Eylem & Doğrulama
        // Eğer RabbitMQ kapalıysa PublishAsync metodu hata fırlatır ve test başarısız olur.
        await _distributedEventBus.PublishAsync(testEvent);

        // Buraya kadar geldiyse bağlantı başarılıdır.
        testEvent.ShouldNotBeNull();
    }
}