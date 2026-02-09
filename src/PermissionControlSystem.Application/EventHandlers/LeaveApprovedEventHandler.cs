using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using Polly;
using Polly.CircuitBreaker;
using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers
{
    public class LeaveApprovedEventHandler : IDistributedEventHandler<LeaveApprovedEto>, ITransientDependency
    {
        private readonly ILogger<LeaveApprovedEventHandler> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IRepository<IncomingMessage, Guid> _incomingMessageRepository;

        private static readonly AsyncCircuitBreakerPolicy _mailCircuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30),
                onBreak: (ex, time) => Console.WriteLine($"⚠️ Mail servisi kesildi! {time.TotalSeconds} sn bekle."),
                onReset: () => Console.WriteLine("✅ Mail servisi düzeldi.")
            );

        public LeaveApprovedEventHandler(
            ILogger<LeaveApprovedEventHandler> logger,
            IEmailSender emailSender,
            IRepository<IncomingMessage, Guid> incomingMessageRepository)
        {
            _logger = logger;
            _emailSender = emailSender;
            _incomingMessageRepository = incomingMessageRepository;
        }

        public async Task HandleEventAsync(LeaveApprovedEto eventData)
        {
            _logger.LogInformation($"[RABBITMQ] LeaveApproved tetiklendi. LeaveId: {eventData.LeaveRequestId}");

            if (await _incomingMessageRepository.AnyAsync(x => x.EventId == eventData.EventId))
            {
                _logger.LogWarning($"[INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). İşlem atlanıyor.");
                return;
            }

            var toEmail = "eren1coskun11@gmail.com";
            var subject = "İzin Talebiniz Onaylandı!";
            var body = $"Sayın çalışanımız, izin talebiniz onaylanmıştır.\nYönetici Mesajı: {eventData.ManagerResponse}";

            try
            {
                await _mailCircuitBreaker.ExecuteAsync(async () =>
                {
                    await _emailSender.SendAsync(toEmail, subject, body);
                });

                _logger.LogInformation("📧 Mail başarıyla gönderildi.");

                await _incomingMessageRepository.InsertAsync(
                    new IncomingMessage(eventData.EventId, "LeaveApproved"),
                    autoSave: true
                );
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