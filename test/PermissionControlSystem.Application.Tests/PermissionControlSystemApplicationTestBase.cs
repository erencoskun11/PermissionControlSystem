using Volo.Abp.Modularity;

namespace PermissionControlSystem;

public abstract class PermissionControlSystemApplicationTestBase<TStartupModule> : PermissionControlSystemTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
