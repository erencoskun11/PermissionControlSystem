using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using PermissionControlSystem.Policies;
using Polly;
using Polly.CircuitBreaker;
using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Caching;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents
{
    public class LeaveApprovedEventHandler : IDistributedEventHandler<LeaveApprovedEto>, ITransientDependency
    {
        private readonly ILogger<LeaveApprovedEventHandler> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IDistributedCache<string, string> _inboxCache;
        private readonly IConfiguration _configuration;
        private static readonly ResiliencePipeline _mailPipeline = MailResiliencePolicy.GetPipeline();


        public LeaveApprovedEventHandler(
            ILogger<LeaveApprovedEventHandler> logger,
            IEmailSender emailSender,
            IDistributedCache<string, string> inboxCache,
            IConfiguration configuration)
        {
            _logger = logger;
            _emailSender = emailSender;
            _inboxCache = inboxCache;
            _configuration = configuration;
        }

        public async Task HandleEventAsync(LeaveApprovedEto eventData)
        {
            _logger.LogInformation($"[RABBITMQ] LeaveApproved tetiklendi. LeaveId: {eventData.LeaveRequestId}");

            var inboxKey = $"Inbox_LeaveApproved_{eventData.EventId}";
            var alreadyProcessed = await _inboxCache.GetAsync(inboxKey);

            if (alreadyProcessed != null)
            {
                _logger.LogWarning($"[INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). İşlem atlanıyor.");
                return;
            }

            var toEmail = _configuration["Settings:Email:ManagerEmail"];
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new InvalidOperationException("Settings:Email:ManagerEmail configuration is required.");
            }
            var subject = "İzin Talebiniz Onaylandı!";
            var body = $"Sayın çalışanımız, izin talebiniz onaylanmıştır.\nYönetici Mesajı: {eventData.ManagerResponse}";

            try
            {
                await _mailPipeline.ExecuteAsync(async cancellationToken =>
                {
                    await _emailSender.SendAsync(toEmail, subject, body);
                });

                _logger.LogInformation("📧 Mail başarıyla gönderildi.");

                await _inboxCache.SetAsync(inboxKey, "Processed", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });
            }
            catch (BrokenCircuitException)
            {
                _logger.LogError("🛑 Mail servisi devre dışı (Circuit Breaker). Mesaj kuyruğa geri dönecek.");
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Bir hata oluştu.");
                throw; 
            }
        }
    }
}