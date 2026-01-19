using Volo.Abp.Modularity;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemDomainModule),
    typeof(PermissionControlSystemTestBaseModule)
)]
public class PermissionControlSystemDomainTestModule : AbpModule
{

}
