using Volo.Abp.Modularity;

namespace PermissionControlSystem;

/* Inherit from this class for your domain layer tests. */
public abstract class PermissionControlSystemDomainTestBase<TStartupModule> : PermissionControlSystemTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
