using Microsoft.Extensions.Caching.StackExchangeRedis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq; 
using Volo.Abp.Modularity;
using Volo.Abp.RabbitMQ;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemApplicationModule),
    typeof(PermissionControlSystemDomainTestModule),
    typeof(AbpEventBusRabbitMqModule) ,
    typeof(AbpCachingStackExchangeRedisModule) 
    )]
public class PermissionControlSystemApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 2. RabbitMQ Bağlantı Ayarlarını Test İçin Zorla
        Configure<AbpRabbitMqOptions>(options =>
        {
            options.Connections.Default.HostName = "localhost";
            options.Connections.Default.UserName = "guest";
            options.Connections.Default.Password = "guest";
        });

        // Testlerde Event Bus'ın gerçek RabbitMQ olmasını sağla
        Configure<AbpRabbitMqEventBusOptions>(options =>
        {
            options.ClientName = "Test_Client";
            options.ExchangeName = "Test_Exchange";
        });
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "PermissionSystemTest:";
        });

        Configure<RedisCacheOptions>(options =>
        {
            // Docker'da veya yerelde Redis genelde bu portta çalışır
            options.Configuration = "127.0.0.1:6379";
        });




    }
}