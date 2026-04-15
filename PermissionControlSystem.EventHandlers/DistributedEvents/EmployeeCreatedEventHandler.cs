using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using PermissionControlSystem.Policies;
using Polly;
using Polly.CircuitBreaker;
using System;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents
{
    public class EmployeeCreatedEventHandler : IDistributedEventHandler<EmployeeCreatedEto>, ITransientDependency
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmployeeCreatedEventHandler> _logger;
        private readonly IDistributedCache<string, string> _inboxCache;

        private static readonly ResiliencePipeline _mailPipeline = MailResiliencePolicy.GetPipeline();

        public EmployeeCreatedEventHandler(
            IEmailSender emailSender,
            ILogger<EmployeeCreatedEventHandler> logger,
            IDistributedCache<string, string> inboxCache)
        {
            _emailSender = emailSender;
            _logger = logger;
            _inboxCache = inboxCache;
        }

        public async Task HandleEventAsync(EmployeeCreatedEto eventData)
        {
            var inboxKey = $"Inbox_EmployeeCreated_{eventData.EventId}";
            var alreadyProcessed = await _inboxCache.GetAsync(inboxKey);

            if (alreadyProcessed != null)
            {
                _logger.LogWarning($"[INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). İşlem atlanıyor.");
                return;
            }

            _logger.LogInformation($"Yeni personel eklendi, hoş geldin maili gönderiliyor: {eventData.FullName}");

            var subject = "Aramıza Hoş Geldin! 🚀";
            var body = $@"
                <h3>Merhaba {eventData.FullName},</h3>
                <p>Şirketimize <strong>{eventData.Position}</strong> pozisyonunda katıldığın için çok mutluyuz.</p>
                <p>İzin sistemine giriş yapmak ve taleplerini yönetmek için portalımızı kullanabilirsin.</p>
                <br>
                <p>Başarılar dileriz,</p>
                <p><strong>İnsan Kaynakları Departmanı</strong></p>";

            try
            {
                // 🔥 Zırhımız merkezi sınıftan geliyor ve burada uygulanıyor
                await _mailPipeline.ExecuteAsync(async cancellationToken =>
                {
                    await _emailSender.SendAsync(eventData.Email, subject, body, isBodyHtml: true);
                });

                _logger.LogInformation("📧 Hoşgeldin maili gönderildi: {Email}", eventData.Email);

                await _inboxCache.SetAsync(inboxKey, "Processed", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });
            }
            catch (BrokenCircuitException)
            {
                _logger.LogError("🛑 Mail devre dışı. Mesaj kuyruğa geri dönecek.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Hoşgeldin maili gönderilemedi: {Email}", eventData.Email);
                throw;
            }
        }
    }
}