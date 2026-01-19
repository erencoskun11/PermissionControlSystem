using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper; // AutoMapper kütüphanesi
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
    typeof(AbpAutoMapperModule) // Modül bağımlılığı eklendi
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
}