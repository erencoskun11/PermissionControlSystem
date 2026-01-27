using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using PermissionControlSystem.Workers;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper; 
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(PermissionControlSystemApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpEmailingModule) 
    )]
public class PermissionControlSystemApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // AutoMapper'ı aktif ediyoruz
        context.Services.AddAutoMapperObjectMapper<PermissionControlSystemApplicationModule>();

        // Mapping ayarlarını bu modülden okumasını söylüyoruz
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<PermissionControlSystemApplicationModule>();
        }); 
    }
    // --- BU METODU EKLE VEYA GÜNCELLE ---
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // Arka plan işçisini başlat
        await context.AddBackgroundWorkerAsync<PendingLeavesCheckerWorker>();
    }




}