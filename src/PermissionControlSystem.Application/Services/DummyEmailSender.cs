using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.Services // Senin namespace'in neyse onu kullan
{
    // 🔥 ITransientDependency: ABP'nin bu sınıfı otomatik olarak Dependency Injection (DI) havuzuna eklemesini sağlar!
    public class DummyEmailSender : IEmailSender, ITransientDependency
    {
        private readonly ILogger<DummyEmailSender> _logger;

        public DummyEmailSender(ILogger<DummyEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Gerçekten mail atmıyoruz, sadece konsola "Mail Atıldı" yazıp Hangfire'ı kandırıyoruz 😎
            _logger.LogInformation($"📧 [SAHTE MAİL GÖNDERİLDİ] Kime: {email} | Konu: {subject}");

            return Task.CompletedTask;
        }
    }
}