using Volo.Abp.Modularity;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemApplicationModule),
    typeof(PermissionControlSystemDomainTestModule)
)]
public class PermissionControlSystemApplicationTestModule : AbpModule
{

}
