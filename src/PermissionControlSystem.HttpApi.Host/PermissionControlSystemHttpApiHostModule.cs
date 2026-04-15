using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Redis.StackExchange;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http; // 🔥 KALKAN İÇİN EKLENDİ
using Microsoft.AspNetCore.RateLimiting; // 🔥 KALKAN İÇİN EKLENDİ
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using PermissionControlSystem.EntityFrameworkCore;
using PermissionControlSystem.EventHandlers;
using PermissionControlSystem.Events;
using PermissionControlSystem.Jobs;
using PermissionControlSystem.MultiTenancy;
using PermissionControlSystem.SignalR;
using Prometheus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json; // 🔥 JSON SERIALIZER İÇİN EKLENDİ
using System.Text.Json.Serialization;
using System.Threading.RateLimiting; // 🔥 KALKAN İÇİN EKLENDİ
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace PermissionControlSystem;

[DependsOn(
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(PermissionControlSystemHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(PermissionControlSystemApplicationModule),
    typeof(PermissionControlSystemEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(PermissionControlSystemEventHandlersModule),
    typeof(AbpBackgroundJobsHangfireModule),
    typeof(AbpSwashbuckleModule)
)]
public class PermissionControlSystemHttpApiHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("PermissionControlSystem");
                options.UseLocalServer();
                options.UseAspNetCore();
            });

            builder.AddServer(options =>
            {
                options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
                options.UseAspNetCore().EnableTokenEndpointPassthrough();
                options.UseAspNetCore().EnableUserInfoEndpointPassthrough();
            });
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        // =====================================================================
        // 🔥 DDOS KALKANI: IP Bazlı Kusursuz Hız Sınırlandırma Sistemi
        // =====================================================================
        context.Services.AddRateLimiter(options =>
        {
            // 1. GENEL KALKAN: Saniyede binlerce istek atıp sistemi yoranlara karşı
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                // 🔥 Proxy (Nginx vb.) arkasındayken gerçek IP'yi bul
                var realIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: realIp,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100, // 1 dakika içinde 100 istek
                        QueueLimit = 0,    // Bekletme, anında reddet!
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            // 2. ÖZEL KALKAN (LOGIN İÇİN): Brute-Force (Şifre Deneme) saldırılarına karşı
            options.AddPolicy("LoginPolicy", httpContext =>
            {
                var realIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: realIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, // 1 dakikada sadece 5 deneme hakkı!
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            // 3. SINIRI AŞANLARA VERİLECEK CEVAP (ABP Formatına Uygun JSON)
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                var abpErrorResponse = new
                {
                    error = new
                    {
                        code = "429",
                        message = "Saldırı Kalkanı Devrede: Çok fazla istek attınız. Lütfen 1 dakika bekleyip tekrar deneyin.",
                        details = "Rate limit exceeded."
                    }
                };

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(abpErrorResponse),
                    cancellationToken: token);
            };
        });

        // 1. HANGFIRE KONFİGÜRASYONU
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
        });

        context.Services.AddHangfire(config =>
        {
            var redisConnectionString = configuration["Redis:Configuration"];
            config.UseRedisStorage(redisConnectionString);
        });

        context.Services.AddControllers();

        Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.WriteIndented = true;
        });

        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "PermissionSystem:";
        });

        // =====================================================================
        // 🧹 TERTEMİZ ABP RABBITMQ KONFİGÜRASYONU
        // =====================================================================
        Configure<AbpRabbitMqEventBusOptions>(options =>
        {
            options.ClientName = "PermissionSystem_Client";
            options.ExchangeName = "PermissionSystem_Exchange";
        });

        Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
        {
            options.Filters.AddService<PermissionControlSystem.Filters.NotificationActionFilter>();
        });

        // Yazdığımız özel VIP İşçisini sisteme dahil ediyoruz
        context.Services.AddHostedService<Workers.VipLeaveRequestWorker>();

        // =====================================================================
        // 🏥 SENIOR DOKUNUŞU: SİSTEM SAĞLIĞI (HEALTH CHECKS)
        // =====================================================================
        string rabbitUrl = $"amqp://{configuration["RabbitMQ:Connections:Default:UserName"]}:{configuration["RabbitMQ:Connections:Default:Password"]}@{configuration["RabbitMQ:Connections:Default:HostName"]}:{configuration["RabbitMQ:Connections:Default:Port"]}/{configuration["RabbitMQ:Connections:Default:VirtualHost"]}".Replace("///", "/");

        context.Services.AddHealthChecks()
            .AddNpgSql(configuration["ConnectionStrings:Default"] ?? "", name: "PostgreSQL Database", tags: new[] { "db", "sql", "critical" })
            .AddRedis(
                redisConnectionString: configuration["Redis:Configuration"] ?? "",
                name: "Redis Cache",
                tags: new[] { "cache", "redis", "performance" })
            .AddElasticsearch(options =>
                options.UseServer(configuration["Elasticsearch:Url"] ?? string.Empty)
                    .UseBasicAuthentication(
                        configuration["Elasticsearch:UserName"] ?? string.Empty,
                        configuration["Elasticsearch:Password"] ?? string.Empty),
                name: "Elasticsearch",
                tags: new[] { "search", "database", "analytics" })
            .AddRabbitMQ(
                async sp =>
                {
                    var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitUrl) };
                    return await factory.CreateConnectionAsync();
                },
                name: "RabbitMQ Message Broker",
                tags: new[] { "queue", "rabbitmq", "events" });

        ConfigureAuthentication(context);
        ConfigureBundles();
        ConfigureUrls(configuration);
        ConfigureConventionalControllers();
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
        ConfigureSwaggerServices(context, configuration);
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
            options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<PermissionControlSystemDomainSharedModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}PermissionControlSystem.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<PermissionControlSystemDomainModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}PermissionControlSystem.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<PermissionControlSystemApplicationContractsModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}PermissionControlSystem.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<PermissionControlSystemApplicationModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}PermissionControlSystem.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(PermissionControlSystemApplicationModule).Assembly);
        });
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOAuth(
            configuration["AuthServer:Authority"]!,
            new Dictionary<string, string>
            {
                    {"PermissionControlSystem", "PermissionControlSystem API"}
            },
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "PermissionControlSystem API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(configuration["App:CorsOrigins"]?
                        .Split(",", StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.RemovePostFix("/"))
                        .ToArray() ?? Array.Empty<string>())
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        // 1. ABP Barkodu burada üretiyor/yakalıyor
        app.UseCorrelationId();

        // 2. DEDEKTİF KURYEMİZ
        app.UseMiddleware<PermissionControlSystem.Middlewares.CorrelationIdEnricherMiddleware>();

        app.UseStaticFiles();
        app.MapAbpStaticAssets();

        app.UseRouting();

        // 🔥 DDOS KALKANINI DEVREYE SOKTUK
        app.UseRateLimiter();

        app.UseHttpMetrics();
        app.UseCors();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "PermissionControlSystem API");
            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            c.OAuthScopes("PermissionControlSystem");
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();

        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapHub<NotificationHub>("/my-notification-hub");

            endpoints.MapHangfireDashboard("/hangfire", new DashboardOptions
            {
                // 🔥 HANGFIRE GÜVENLİĞİ: Sadece canlıda şifre sor, geliştirme ortamında serbest bırak!
                Authorization = env.IsDevelopment()
                    ? Array.Empty<IDashboardAuthorizationFilter>()
                    : new[] { new HangfireDashboardCustomAuthorizationFilter() },
                DashboardTitle = "İzin Sistemi Kontrol Paneli",
                IgnoreAntiforgeryToken = true
            });

            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            endpoints.MapMetrics();
        });

        var recurringJobManager = context.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<LeaveReminderJob>(
            "Daily_Leave_Check",
            job => job.CheckOldLeavesAsync(),
            Cron.Daily
        );
    }
}

// =====================================================================
// 👇 HANGFIRE GÜVENLİK FİLTRESİ (AKTİF EDİLDİ)
// =====================================================================
public class HangfireDashboardCustomAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var context2 = context.GetHttpContext();
        var configuration = context2.RequestServices.GetRequiredService<IConfiguration>();
        var expectedUsername = configuration["Hangfire:Dashboard:UserName"];
        var expectedPassword = configuration["Hangfire:Dashboard:Password"];

        if (string.IsNullOrWhiteSpace(expectedUsername) || string.IsNullOrWhiteSpace(expectedPassword))
        {
            context2.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        // Basic Auth Header Kontrolü
        var header = context2.Request.Headers["Authorization"].ToString();

        if (!string.IsNullOrWhiteSpace(header))
        {
            var authValues = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(header);

            if ("Basic".Equals(authValues.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(authValues.Parameter))
                {
                    return false;
                }

                var parameter = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authValues.Parameter));
                var parts = parameter.Split(':');

                if (parts.Length > 1)
                {
                    string username = parts[0];
                    string password = parts[1];

                    if (username == expectedUsername && password == expectedPassword)
                    {
                        return true;
                    }
                }
            }
        }

        context2.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
        context2.Response.StatusCode = 401; 

        return false;
    }
}