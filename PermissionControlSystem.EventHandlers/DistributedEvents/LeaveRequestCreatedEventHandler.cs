using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using PermissionControlSystem.Policies;
using PermissionControlSystem.SignalR;
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
    public class LeaveRequestCreatedEventHandler
        : IDistributedEventHandler<LeaveRequestCreatedEto>, ITransientDependency
    {
        private readonly ILogger<LeaveRequestCreatedEventHandler> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache<string, string> _inboxCache;


        private static readonly ResiliencePipeline _mailPipeline = MailResiliencePolicy.GetPipeline();

        public LeaveRequestCreatedEventHandler(
            ILogger<LeaveRequestCreatedEventHandler> logger,
            IEmailSender emailSender,
            IHubContext<NotificationHub> hubContext,
            IConfiguration configuration,
            IDistributedCache<string, string> inboxCache)
        {
            _logger = logger;
            _emailSender = emailSender;
            _hubContext = hubContext;
            _configuration = configuration;
            _inboxCache = inboxCache;
        }

        public async Task HandleEventAsync(LeaveRequestCreatedEto eventData)
        {
            // 1. 🛡️ INBOX KONTROLÜ: Gümrükten geçiş izni var mı?
            var inboxKey = $"Inbox_LeaveCreated_{eventData.EventId}";
            var alreadyProcessed = await _inboxCache.GetAsync(inboxKey);

            if (alreadyProcessed != null)
            {
                _logger.LogWarning("[INBOX-KORUMASI] EventId: {EventId} zaten islendi, tekrar engellendi.", eventData.EventId);
                return;
            }

            _logger.LogInformation("[IZIN-TALEBI] Yeni talep geldi. Personel: {EmployeeName}", eventData.EmployeeName);

            // 2. 📢 SIGNALR BİLDİRİMİ (Side Effect - Patlarsa maili engellemesin)
            try
            {
                var popupMessage = $"📢 {eventData.EmployeeName} {eventData.Message} ({eventData.StartDate:d} - {eventData.EndDate:d})";
                await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", popupMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SIGNALR] Bildirim gonderilemedi ama mail sureci devam ediyor.");
            }

            // 3. 📧 MAIL GÖNDERİMİ - POLLY KALKANIYLA
            var adminEmail = _configuration["Settings:Email:AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                throw new InvalidOperationException("Settings:Email:AdminEmail configuration is required.");
            }
            var body = $"Personel: {eventData.EmployeeName}\nSebep: {eventData.Reason}\nTarih: {eventData.StartDate:d} - {eventData.EndDate:d}";

            try
            {
                // 🛡️ Madde 1: Polly Retry + Circuit Breaker burada devreye giriyor
                await _mailPipeline.ExecuteAsync(async cancellationToken =>
                {
                    await _emailSender.SendAsync(adminEmail, "Yeni Izin Talebi", body);
                });

                _logger.LogInformation("[MAIL] Admin maili basariyla gonderildi.");

                // 🔥 4. İŞLEM MÜHRÜ (INBOX SET): Her şey bittikten sonra "İşlendi" damgasını basıyoruz.
                // Claude'un uyarısı: Bunu try-catch dışına veya mail başarısından hemen sonraya, 
                // ama tüm business logic bittikten sonraya almak en güvenlisidir.
                await _inboxCache.SetAsync(inboxKey, "Processed", new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // 🛡️ 30 gün boyunca hatırla (Daha güvenli)
                });
            }
            catch (BrokenCircuitException)
            {
                // Sigorta attıysa mesajı RabbitMQ'ya geri gönder (Nack) ki sonra tekrar denensin
                _logger.LogError("[POLLY] Mail devresi kesildi (Circuit Broken). Mesaj kuyruga iade ediliyor.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HATA] Mail gonderimi sirasinda beklenmedik hata.");
                throw;
            }
        }
    }
}