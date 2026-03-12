using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Events;
using Volo.Abp.Emailing;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using PermissionControlSystem.SignalR;
using PermissionControlSystem.EventHandlers.DistributedEvents;

namespace PermissionControlSystem.EventHandlers
{
    // 🔥 SENIOR FIX: Miras (Inheritance) SİLİNDİ! 
    // Tüm bağımlılıklar mock'landığı için ABP'yi ayağa kaldırmaya (TestBase) hiç gerek yok.
    // Artık bu %100 saf, veritabanı veya Redis istemeyen ışık hızında bir Unit Test!
    public class LeaveRequestCreatedEventHandlerTests
    {
        private readonly LeaveRequestCreatedEventHandler _handler;
        private readonly IEmailSender _fakeEmailSender;
        private readonly IHubContext<NotificationHub> _fakeHubContext;

        public LeaveRequestCreatedEventHandlerTests()
        {
            _fakeEmailSender = Substitute.For<IEmailSender>();

            _fakeHubContext = Substitute.For<IHubContext<NotificationHub>>();
            var fakeClients = Substitute.For<IHubClients>();
            var fakeClientProxy = Substitute.For<IClientProxy>();

            fakeClients.All.Returns(fakeClientProxy);
            _fakeHubContext.Clients.Returns(fakeClients);

            _handler = new LeaveRequestCreatedEventHandler(
                NullLogger<LeaveRequestCreatedEventHandler>.Instance,
                _fakeEmailSender,
                _fakeHubContext
            );
        }

        [Fact]
        public async Task HandleEventAsync_Should_Send_Email()
        {
            // ARRANGE
            var eventData = new LeaveRequestCreatedEto
            {
                LeaveRequestId = Guid.NewGuid(),
                StaffId = Guid.NewGuid(),
                EmployeeName = "Ahmet Yılmaz",
                Message = "Yeni İzin Talebi",
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(5),
                Reason = "Yıllık İzin"
            };

            // ACT
            await _handler.HandleEventAsync(eventData);

            // ASSERT
            // Mail içeriğinde StaffId yerine EmployeeName ("Ahmet Yılmaz") arıyoruz.
            // Ayrıca SendAsync'in beklediği 5 parametreyi tam veriyoruz.
            await _fakeEmailSender.Received(1).SendAsync(
                Arg.Any<string>(), // to (Kime)
                Arg.Any<string>(), // subject (Konu)
                Arg.Is<string>(body => body.Contains(eventData.EmployeeName) && body.Contains(eventData.Reason)), // body (İçerik)
                Arg.Any<bool>(),   // isBodyHtml (HTML mi?)
                null               // ek argümanlar
            );
        }
    }
}