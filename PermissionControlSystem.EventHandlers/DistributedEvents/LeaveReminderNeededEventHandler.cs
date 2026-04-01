using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using PermissionControlSystem.Policies;
using Polly.CircuitBreaker;
using Polly.Wrap;
using System;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents
{
    public class LeaveReminderNeededEventHandler : IDistributedEventHandler<LeaveReminderNeededEto>, ITransientDependency
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LeaveReminderNeededEventHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache<string, string> _inboxCache; 

        private static readonly AsyncPolicyWrap _mailPolicy = MailResiliencePolicy.GetPolicy();
        public LeaveReminderNeededEventHandler(
            IEmailSender emailSender,
            ILogger<LeaveReminderNeededEventHandler> logger,
            IConfiguration configuration,
            IDistributedCache<string,string> inboxCache)
        {
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
            _inboxCache = inboxCache;
        }

        public async Task HandleEventAsync(LeaveReminderNeededEto eventData)
        {
          //🔥 INBOX KONTROLÜ: Bu hatırlatma zaten atıldı mı?
            var inboxKey = $"Inbox_Reminder_{eventData.LeaveRequestId}";
            var alreadyProcessed = await _inboxCache.GetAsync(inboxKey);

            if (alreadyProcessed != null)
            {
                _logger.LogWarning("🛑 [INBOX KORUMASI] Hatırlatma maili LeaveRequestId: {LeaveRequestId} için zaten atılmış!", eventData.LeaveRequestId);
                return;
            }


            _logger.LogInformation($"[RABBITMQ] Leave reminder needed for LeaveRequestId: {eventData.LeaveRequestId}");

            var emailSubject = $"🚨 Action Needed: Leave Request #{eventData.LeaveRequestId.ToString().Substring(0, 8)}...";
            var emailBody = $@"
                <h3>Pending Leave Request Warning</h3>
                <p>The leave request for Employee <b>{eventData.EmployeeId}</b> has been pending since {eventData.CreationTime}.</p>
                <p>Please log in to the system to approve or reject it.</p>
                <br/>
                <small>Sent automatically by Permission Control System</small>
            ";

            var managerEmail = _configuration["Settings:Email:ManagerEmail"] ?? "eren1coskun11@gmail.com";

            try
            {
                // 🔥 POLLY İLE GÜVENLİ GÖNDERİM
                await _mailPolicy.ExecuteAsync(async () =>
                {
                    await _emailSender.SendAsync(managerEmail, emailSubject, emailBody, isBodyHtml: true);
                });

                // 🔥 BAŞARILIYSA INBOX'A YAZ
                await _inboxCache.SetAsync(inboxKey, "Processed", new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });
            }
            catch (BrokenCircuitException)
            {
                _logger.LogError("🛑 Mail devre dışı. Hatırlatma kuyruğa geri dönecek.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Hatırlatma maili gönderilemedi.");
                throw;
            }
        
    }
    }
}