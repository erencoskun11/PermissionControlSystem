using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace PermissionControlSystem;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpBackgroundJobsAbstractionsModule)
)]
public class PermissionControlSystemTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 🔥 ADIM 1: Redis hatasını çözmek için konfigürasyonu en başta "MOCK"luyoruz
        var mockConfiguration = new Dictionary<string, string>
        {
            {"Redis:Configuration", "127.0.0.1:6379"},
            {"Redis:IsEnabled", "false"}
        };

        // Bu satır, appsettings.json dosyasındaki eksikliği bellek üzerinden tamamlar
        context.Services.ReplaceConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(mockConfiguration)
                .Build()
        );

        // 🔥 ADIM 2: Diğer konfigürasyonlara devam ediyoruz
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });

        context.Services.AddAlwaysAllowAuthorization();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        SeedTestData(context);
    }

    private static void SeedTestData(ApplicationInitializationContext context)
    {
        AsyncHelper.RunSync(async () =>
        {
            using (var scope = context.ServiceProvider.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IDataSeeder>()
                    .SeedAsync();
            }
        });
    }
}