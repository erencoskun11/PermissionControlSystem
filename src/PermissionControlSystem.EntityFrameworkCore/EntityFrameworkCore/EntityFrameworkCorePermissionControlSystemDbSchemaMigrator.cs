using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermissionControlSystem.Data;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.EntityFrameworkCore;

public class EntityFrameworkCorePermissionControlSystemDbSchemaMigrator
    : IPermissionControlSystemDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCorePermissionControlSystemDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the PermissionControlSystemDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<PermissionControlSystemDbContext>()
            .Database
            .MigrateAsync();
    }
}
