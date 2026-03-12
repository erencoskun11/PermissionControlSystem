using Hangfire; // 👈 Bu eksik olduğu için hata alıyordun
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.DependencyInjection;
using PermissionControlSystem.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.BackgroundJobs.Hangfire;

namespace PermissionControlSystem.Worker
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(PermissionControlSystemEntityFrameworkCoreModule),
        typeof(PermissionControlSystemDomainModule),
        typeof(AbpBackgroundJobsHangfireModule) // ABP Hangfire modülünü ekledik
    )]
    public class PermissionControlSystemWorkerModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();

            // 1. Hangfire Konfigürasyonu (Redis)
            context.Services.AddHangfire(config =>
            {
                var redisConn = configuration["Redis:Configuration"];
                config.UseRedisStorage(redisConn);
            });

            // 2. Hangfire Server'ı Başlat (Bu proje artık bir İŞÇİ)
            context.Services.AddHangfireServer(options =>
            {
                options.WorkerCount = Environment.ProcessorCount * 2; // İşlemci gücüne göre ayarlar
                options.Queues = new[] { "default" };
            });
        }
    }
}