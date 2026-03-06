using Volo.Abp.Modularity;

namespace PermissionControlSystem.EventHandlers
{
    [DependsOn(
        typeof(PermissionControlSystemEventHandlersModule), 
        typeof(PermissionControlSystemDomainTestModule)     
    )]
    public class PermissionControlSystemEventHandlersTestModule : AbpModule
    {
    }
}