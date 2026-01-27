using Microsoft.EntityFrameworkCore;
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
using Npgsql; 

namespace PermissionControlSystem.EventHandlers
{
    public class LeaveApprovedEventHandler : IDistributedEventHandler<LeaveApprovedEto>, ITransientDependency
    {
        private readonly ILogger<LeaveApprovedEventHandler> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IRepository<IncomingMessage, Guid> _incomingMessageRepository;

        private static readonly AsyncCircuitBreakerPolicy _mailCircuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                3,
                TimeSpan.FromSeconds(30),
                onBreak: (ex, time) =>
                {
                    Console.WriteLine($"⚠️ Mail servisi cevap vermiyor! {time.TotalSeconds} sn bekleme.");
                },
                onReset: () =>
                {
                    Console.WriteLine("✅ Mail servisi tekrar devrede.");
                });

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
            _logger.LogInformation($"[RABBITMQ] LeaveApproved işlendi. LeaveId: {eventData.LeaveRequestId}");

            var toEmail = "eren1coskun11@gmail.com";
            var subject = "İzin Talebiniz Onaylandı!";
            var body = "Sayın çalışanımız, izin talebiniz onaylanmıştır.";

            try
            {
                // 1. Mail gönder
                await _mailCircuitBreaker.ExecuteAsync(async () =>
                {
                    await _emailSender.SendAsync(toEmail, subject, body);
                });

                _logger.LogInformation("📧 Mail başarıyla gönderildi.");

                // 2. Inbox kaydı → idempotency burada
                try
                {
                    await _incomingMessageRepository.InsertAsync(
                        new IncomingMessage(eventData.EventId, "LeaveApproved"),
                        autoSave: true
                    );
                }
                catch (DbUpdateException ex)
                {
                    // PostgreSQL unique constraint
                    if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                    {
                        _logger.LogWarning($"[INBOX] Mesaj zaten işlenmişti (EventId: {eventData.EventId}). Atlandı.");
                        return;
                    }

                    throw;
                }
            }
            catch (BrokenCircuitException)
            {
                _logger.LogError("🛑 Mail servisi sigortaya girdi. Mesaj tekrar denenecek.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LeaveApprovedEventHandler hata aldı.");
                throw;
            }
        }
    }
}
