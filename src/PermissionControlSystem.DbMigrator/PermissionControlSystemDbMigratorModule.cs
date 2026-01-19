using PermissionControlSystem.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace PermissionControlSystem.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(PermissionControlSystemEntityFrameworkCoreModule),
    typeof(PermissionControlSystemApplicationContractsModule)
    )]
public class PermissionControlSystemDbMigratorModule : AbpModule
{
}
