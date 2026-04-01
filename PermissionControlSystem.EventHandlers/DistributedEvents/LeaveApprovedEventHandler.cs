using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents
{
    public class LeaveApprovedEventHandler : IDistributedEventHandler<LeaveApprovedEto>, ITransientDependency
    {
        private readonly ILogger<LeaveApprovedEventHandler> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IRepository<IncomingMessage, Guid> _incomingMessageRepository;
        private readonly IConfiguration _configuration;


        // ✅ Retry: 3 deneme, 2s - 4s - 8s bekleyerek (exponential backoff)
        private static readonly AsyncPolicy _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not BrokenCircuitException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, wait, attempt, _) =>
                    Console.WriteLine($"🔁 Mail retry {attempt}/3 — {wait.TotalSeconds}s sonra tekrar. Hata: {ex.Message}")
            );

        // ✅ Circuit Breaker: 3 ardışık hata sonrası 30s devre dışı
        private static readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, time) =>
                    Console.WriteLine($"⚠️ Mail servisi kesildi! {time.TotalSeconds}s bekle."),
                onReset: () =>
                    Console.WriteLine("✅ Mail servisi düzeldi."),
                onHalfOpen: () =>
                    Console.WriteLine("🔶 Mail servisi test ediliyor...")
            );

        // ✅ İkisini birleştir: önce Retry, sonra Circuit Breaker
        private static readonly AsyncPolicyWrap _mailPolicy =
            Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);


        public LeaveApprovedEventHandler(
            ILogger<LeaveApprovedEventHandler> logger,
            IEmailSender emailSender,
            IRepository<IncomingMessage, Guid> incomingMessageRepository,
            IConfiguration configuration)
        {
            _logger = logger;
            _emailSender = emailSender;
            _incomingMessageRepository = incomingMessageRepository;
            _configuration = configuration;
        }

        public async Task HandleEventAsync(LeaveApprovedEto eventData)
        {
            _logger.LogInformation($"[RABBITMQ] LeaveApproved tetiklendi. LeaveId: {eventData.LeaveRequestId}");

            if (await _incomingMessageRepository.AnyAsync(x => x.EventId == eventData.EventId))
            {
                _logger.LogWarning($"[INBOX] Bu mesaj ZATEN işlenmiş (EventId: {eventData.EventId}). İşlem atlanıyor.");
                return;
            }

            var toEmail = _configuration["Settings:Email:ManagerEmail"] ?? "eren1coskun11@gmail.com";
            var subject = "İzin Talebiniz Onaylandı!";
            var body = $"Sayın çalışanımız, izin talebiniz onaylanmıştır.\nYönetici Mesajı: {eventData.ManagerResponse}";

            try
            {
                await _mailPolicy.ExecuteAsync(async () =>
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