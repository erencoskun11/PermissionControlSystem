using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.Data;

/* This is used if database provider does't define
 * IPermissionControlSystemDbSchemaMigrator implementation.
 */
public class NullPermissionControlSystemDbSchemaMigrator : IPermissionControlSystemDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
