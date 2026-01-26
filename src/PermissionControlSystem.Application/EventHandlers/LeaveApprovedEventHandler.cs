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

        // SİGORTA TANIMI
        private static readonly AsyncCircuitBreakerPolicy _mailCircuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, time) =>
                {
                    Console.WriteLine($"⚠️ DİKKAT: Mail servisi cevap vermiyor! Sigorta attı. {time.TotalSeconds} saniye bekleyeceğiz.");
                },
                onReset: () =>
                {
                    Console.WriteLine("✅ SİGORTA KAPANDI: Mail servisi tekrar devrede.");
                }
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
            // 1. INBOX KONTROLÜ
            var alreadyProcessed = await _incomingMessageRepository.AnyAsync(x => x.EventId == eventData.EventId);
            if (alreadyProcessed)
            {
                _logger.LogWarning($"[INBOX]: Mesaj (ID: {eventData.EventId}) daha önce işlenmiş! Pas geçiliyor. 🛑");
                return;
            }

            _logger.LogInformation($"[RABBITMQ]: İzin {eventData.LeaveRequestId} işleniyor...");

            var toEmail = "eren1coskun11@gmail.com"; 
            var subject = "İzin Talebiniz Onaylandı! 🎉";
            var body = $"Sayın çalışanımız,<br>İzin talebiniz onaylanmıştır.";

            // 2. SİGORTALI İŞLEM (DÜZELTİLEN KISIM BURASI) 👇
            try
            {
                // Mail atma işlemini Sigorta Politikası içine sarıyoruz!
                await _mailCircuitBreaker.ExecuteAsync(async () =>
                {
                    await _emailSender.SendAsync(toEmail, subject, body);
                });

                _logger.LogInformation($"[BAŞARILI]: Mail gönderildi.");

                // 3. INBOX KAYDI (Sadece başarılıysa kaydet)
                await _incomingMessageRepository.InsertAsync(
                    new IncomingMessage(eventData.EventId, "LeaveApproved")
                );
            }
            catch (BrokenCircuitException)
            {
                // Sigorta açıksa (Hala cezalıysak) buraya düşer
                _logger.LogError($"🛑 SİGORTA ATIK: Mail servisine gidilmedi. Mesaj kuyruğa geri bırakılacak.");
                throw; // Hatayı fırlat ki RabbitMQ mesajı silmesin, sonra tekrar denesin.
            }
            catch (Exception ex)
            {
                // Diğer hatalar
                _logger.LogError($"❌ HATA: Mail atılamadı. Detay: {ex.Message}");
                throw; // RabbitMQ tekrar denesin.
            }
        }
    }
}