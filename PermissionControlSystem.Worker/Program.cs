using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;

namespace PermissionControlSystem.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // 1. Autofac Entegrasyonu
            builder.ConfigureContainer(new Volo.Abp.Autofac.AbpAutofacServiceProviderFactory(new Autofac.ContainerBuilder()));

            // 2. Modülü Ekle
            await builder.Services.AddApplicationAsync<PermissionControlSystemWorkerModule>();

            // 3. Host'u Oluştur
            var host = builder.Build();

            // 🛠️ DÜZELTME BURADA:
            // "host.InitializeAbpApplicationAsync()" yerine manuel başlatma yapıyoruz.
            // Bu yöntem her zaman çalışır ve hatasızdır.
            var application = host.Services.GetRequiredService<IAbpApplicationWithExternalServiceProvider>();
            await application.InitializeAsync(host.Services);

            // 4. Uygulamayı Çalıştır
            await host.RunAsync();
        }
    }
}