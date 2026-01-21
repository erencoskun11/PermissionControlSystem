using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;

namespace PermissionControlSystem.EntityFrameworkCore;

[DependsOn(
    typeof(PermissionControlSystemEntityFrameworkCoreModule), // Ana Proje
    typeof(PermissionControlSystemTestBaseModule),            // <-- DÜZELTME: DomainTest YERİNE TestBase olmalı!
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule)     // Feature Management Fix
    )]
public class PermissionControlSystemEntityFrameworkCoreTestModule : AbpModule
{
    private SqliteConnection _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });

        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        ConfigureInMemorySqlite(context.Services);
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();
        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                if (context.ExistingConnection == null)
                {
                    context.DbContextOptions.UseSqlite(_sqliteConnection);
                }
            });
        });
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PermissionControlSystemDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new PermissionControlSystemDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}