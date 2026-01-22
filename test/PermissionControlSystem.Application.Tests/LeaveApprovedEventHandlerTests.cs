using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.EventHandlers;
using PermissionControlSystem.Events;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Uow;
using Xunit;

namespace PermissionControlSystem
{
    public class LeaveApprovedEventHandlerTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        // Alanları sildik, her şeyi metodun içinde taze taze oluşturacağız.
        private readonly IEmailSender _fakeEmailSender;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public LeaveApprovedEventHandlerTests()
        {
            // Bu servisler veritabanı tutmadığı için Constructor'da kalabilir.
            _fakeEmailSender = Substitute.For<IEmailSender>();
            _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
        }

        [Fact]
        public async Task Inbox_Pattern_Should_Prevent_Duplicate_Emails()
        {
            // --- ARRANGE ---
            var eventId = Guid.NewGuid();
            var eventData = new LeaveApprovedEto
            {
                EventId = eventId,
                LeaveRequestId = Guid.NewGuid(),
                ManagerResponse = "Test Onayı"
            };

            // -----------------------------------------------------------
            // --- ACT 1 (İlk Kez Çağırıyoruz) ---
            // -----------------------------------------------------------

            // 👇 ÇÖZÜM: Repository'yi ve Handler'ı BURADA, bu UOW içinde oluşturuyoruz.
            using (var uow = _unitOfWorkManager.Begin())
            {
                // 1. Repository'yi bu kapsam (scope) içinde çağır
                var repo = GetRequiredService<IRepository<IncomingMessage, Guid>>();

                // 2. Handler'ı bu taze repo ile oluştur
                var handler = new LeaveApprovedEventHandler(
                    NullLogger<LeaveApprovedEventHandler>.Instance,
                    _fakeEmailSender,
                    repo
                );

                // 3. İşlemi yap
                await handler.HandleEventAsync(eventData);

                // 4. Kaydet
                await uow.CompleteAsync();
            }

            // --- ASSERT 1 ---
            await _fakeEmailSender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

            // Veritabanı kontrolü (Okuma işlemi için ayrı scope açalım temiz olsun)
            using (var uow = _unitOfWorkManager.Begin())
            {
                var repo = GetRequiredService<IRepository<IncomingMessage, Guid>>();
                var inboxRecord = await repo.FindAsync(eventId);

                inboxRecord.ShouldNotBeNull();
                inboxRecord.EventId.ShouldBe(eventId);
            }

            // -----------------------------------------------------------
            // --- ACT 2 (Simülasyon - İkinci Kez Çağır) ---
            // -----------------------------------------------------------

            using (var uow = _unitOfWorkManager.Begin())
            {
                // Tekrar taze repo ve handler alıyoruz (Gerçek hayatta da her istekte yeni oluşur)
                var repo = GetRequiredService<IRepository<IncomingMessage, Guid>>();
                var handler = new LeaveApprovedEventHandler(
                    NullLogger<LeaveApprovedEventHandler>.Instance,
                    _fakeEmailSender,
                    repo
                );

                await handler.HandleEventAsync(eventData);
                await uow.CompleteAsync();
            }

            // --- ASSERT 2 ---
            // Hala toplamda 1 olmalı
            await _fakeEmailSender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}