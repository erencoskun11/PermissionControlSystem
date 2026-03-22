using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenSearch.Client;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Models;
using PermissionControlSystem.Services;
using System;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.RabbitMQ;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemApplicationModule),
    typeof(PermissionControlSystemDomainTestModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpCachingStackExchangeRedisModule)
)]
public class PermissionControlSystemApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRabbitMqOptions>(options =>
        {
            options.Connections.Default.HostName = "46.224.57.16";
            options.Connections.Default.Port = 5672;
            options.Connections.Default.UserName = "admin";
            options.Connections.Default.Password = "thescappe.123";
            options.Connections.Default.VirtualHost = "eren";
        });

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
            // Test ortamında farklı bir veritabanı (DB 10) kullanıyoruz ki gerçek verilerle karışmasın
            options.Configuration = "46.224.57.16:6379,password=thescappe.123,defaultDatabase=10,abortConnect=false";
        });

        // Email gönderimini testlerde susturuyoruz (Sahte gönderici)
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Substitute.For<IEmailSender>()));

        // Elasticsearch servisini "Dummy" olanla değiştiriyoruz
        context.Services.Replace(ServiceDescriptor.Transient<ElasticSearchService, DummyElasticSearchService>());
    }
}

// 🔥 Dummy Servis: Gerçek bir arama motoruna ihtiyaç duymadan testlerin akmasını sağlar
public class DummyElasticSearchService : ElasticSearchService
{
    public DummyElasticSearchService()
        : base(
            Substitute.For<IOpenSearchClient>(),
            Substitute.For<ILogger<ElasticSearchService>>() // 👈 Logger eklendi!
        )
    { }

    // Status parametreli yeni imza (Interface ile uyumlu)
    public override Task IndexLeaveRequestAsync(LeaveIndexModel model)
    {
        return Task.CompletedTask;
    }
}