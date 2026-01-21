using PermissionControlSystem.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace PermissionControlSystem;

[DependsOn(
    typeof(PermissionControlSystemEntityFrameworkCoreTestModule) // <-- DÜZELTME: Veritabanını buraya bağladık!
    )]
public class PermissionControlSystemDomainTestModule : AbpModule
{
}