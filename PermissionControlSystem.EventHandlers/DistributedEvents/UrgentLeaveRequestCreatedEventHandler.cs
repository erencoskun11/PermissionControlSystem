using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.EventHandlers.DistributedEvents
{
    // 🔥 SADECE VIP (URGENT) MESAJLARI DİNLER
    public class UrgentLeaveRequestCreatedEventHandler : 
        IDistributedEventHandler<UrgentLeaveRequestCreatedEto>, 
        ITransientDependency
    {
        private readonly ILogger<UrgentLeaveRequestCreatedEventHandler> _logger;
        private readonly IEmailSender _emailSender;

        public UrgentLeaveRequestCreatedEventHandler(
            ILogger<UrgentLeaveRequestCreatedEventHandler> logger,
            IEmailSender emailSender)
        {
            _logger = logger;
            _emailSender = emailSender;
        }

        public async Task HandleEventAsync(UrgentLeaveRequestCreatedEto eventData)
        {
            // 1. Loga yazdırıyoruz (Konsolda bunu göreceksin)
            _logger.LogWarning($"🚨 [VIP KANAL] {eventData.EmployeeName} için ACİL izin işlemi başlatıldı!");

            // 2. VIP Maili atıyoruz
            var emailBody = $@"
                <h3>🚨 ACİL İZİN TALEBİ</h3>
                <p><b>Personel:</b> {eventData.EmployeeName}</p>
                <p><b>Gerekçe:</b> {eventData.Reason}</p>
                <p><b>Tarih:</b> {eventData.StartDate:dd.MM.yyyy} - {eventData.EndDate:dd.MM.yyyy}</p>";
            
            await _emailSender.SendAsync(
                "manager@sirket.com", // Test için kendi mailini yazabilirsin
                "🚨 ACİL: Öncelikli İzin Talebi", 
                emailBody, 
                isBodyHtml: true
            );

            _logger.LogInformation($"🚨 [VIP KANAL] {eventData.EmployeeName} için acil mail başarıyla gönderildi!");
        }
    }
}