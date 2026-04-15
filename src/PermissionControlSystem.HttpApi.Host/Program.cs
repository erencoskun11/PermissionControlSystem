using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PermissionControlSystem;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        // 🔥 DEDEKTİF ŞABLONU: Artık her satırda [CID: xxxx] barkodu yazacak
        var logTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [CID: {CorrelationId}] {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext() // Kuryenin çantaya attığı ID buradan okunacak
            .WriteTo.Async(c => c.Console(outputTemplate: logTemplate))
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting PermissionControlSystem.HttpApi.Host.");
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddSource("PermissionControlSystem")
                           .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PermissionControlSystem.API"))
                           .AddAspNetCoreInstrumentation()
                           .AddEntityFrameworkCoreInstrumentation(options =>
                           {
                               options.SetDbStatementForText = true;
                           })
                           .AddHttpClientInstrumentation()
                           .AddOtlpExporter();
                });

            builder.Host.AddAppSettingsSecretsJson()
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

                    configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                 .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                                 .AddEnvironmentVariables();
                })
                .UseAutofac()
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
#if DEBUG
                        .MinimumLevel.Debug()
#else
                        .MinimumLevel.Information()
#endif
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .WriteTo.Async(c => c.File("Logs/logs.txt", outputTemplate: logTemplate))
                        .WriteTo.Async(c => c.Console(outputTemplate: logTemplate));

                    var elasticUrl = context.Configuration["Elasticsearch:Url"];
                    if (!string.IsNullOrWhiteSpace(elasticUrl))
                    {
                        var elasticUser = context.Configuration["Elasticsearch:UserName"];
                        var elasticPassword = context.Configuration["Elasticsearch:Password"];

                        loggerConfiguration.WriteTo.Async(c => c.Elasticsearch(
                            new ElasticsearchSinkOptions(new Uri(elasticUrl))
                            {
                                AutoRegisterTemplate = true,
                                IndexFormat = "permissioncontrolsystem-logs-{0:yyyy.MM}",
                                ModifyConnectionSettings = connection =>
                                {
                                    if (!string.IsNullOrWhiteSpace(elasticUser))
                                    {
                                        connection = connection.BasicAuthentication(elasticUser, elasticPassword);
                                    }

                                    return connection;
                                }
                            }));
                    }
                });

            await builder.AddApplicationAsync<PermissionControlSystemHttpApiHostModule>();

            var app = builder.Build();

            await app.InitializeApplicationAsync();
            await app.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}