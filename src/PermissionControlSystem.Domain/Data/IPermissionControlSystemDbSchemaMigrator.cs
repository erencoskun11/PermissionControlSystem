using System.Threading.Tasks;

namespace PermissionControlSystem.Data;

public interface IPermissionControlSystemDbSchemaMigrator
{
    Task MigrateAsync();
}
