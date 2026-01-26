
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Emailing; // 👈 1. BU EKLENDİ (Mail kütüphanesi)

namespace PermissionControlSystem.EventHandlers;

public class LeaveRequestCreatedEventHandler
    : IDistributedEventHandler<LeaveRequestCreatedEto>,
      ITransientDependency
{
    private readonly ILogger<LeaveRequestCreatedEventHandler> _logger;
    private readonly IEmailSender _emailSender; // 👈 2. MAIL GÖNDERİCİYİ TANIMLADIK

    // Constructor Injection (Dependency Injection)
    public LeaveRequestCreatedEventHandler(
        ILogger<LeaveRequestCreatedEventHandler> logger,
        IEmailSender emailSender) // 👈 3. İÇERİ ALDIK
    {
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task HandleEventAsync(LeaveRequestCreatedEto eventData)
    {
        _logger.LogInformation($"🚀 Yeni İzin Talebi! Mail hazırlanıyor... (Personel: {eventData.StaffId})");

        // 4. MAİL İÇERİĞİNİ HAZIRLA
        string emailSubject = "📢 Yeni İzin Talebi Var!";

        // Şimdilik düz yazı (Daha sonra HTML yapacağız)
        string emailBody = $@"
            Merhaba Yönetici,
            
            Aşağıdaki personel izin talebinde bulundu:
            -----------------------------------------
            Personel ID : {eventData.StaffId}
            Tarihler    : {eventData.StartDate.ToShortDateString()} - {eventData.EndDate.ToShortDateString()}
            Sebep       : {eventData.Reason}
            
            Lütfen sisteme girip onaylayın veya reddedin.
        ";

        // 5. GERÇEK MAİLİ GÖNDER! 📨
        // Not: "yonetici@sirket.com" yerine kendi gerçek mailini yaz ki sonucu görebilesin.
        await _emailSender.SendAsync(
            to: "eren@ornek.com", // 👈 BURAYA KENDİ GERÇEK MAİLİNİ YAZ
            subject: emailSubject,
            body: emailBody
        );

        _logger.LogInformation("✅ Mail başarıyla gönderildi!");
    }
}