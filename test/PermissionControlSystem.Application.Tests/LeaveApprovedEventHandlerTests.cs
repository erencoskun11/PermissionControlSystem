using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Entities;
using PermissionControlSystem.EventHandlers.DistributedEvents;
using PermissionControlSystem.Events;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;
using Microsoft.Extensions.Configuration; // 🔥 YENİ EKLENDİ
using Volo.Abp.Caching;

namespace PermissionControlSystem.EventHandlers
{
    public class LeaveApprovedEventHandlerTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IEmailSender _fakeEmailSender;
        private readonly IDistributedCache<string, string> _fakeInboxCache;

        public LeaveApprovedEventHandlerTests()
        {
            _fakeEmailSender = Substitute.For<IEmailSender>();
            _fakeInboxCache = Substitute.For<IDistributedCache<string, string>>();
            _fakeInboxCache
                .GetAsync(default!, default, default, default)
                .ReturnsForAnyArgs(Task.FromResult<string?>(null));
        }

        [Fact]
        public async Task Inbox_Pattern_Should_Prevent_Duplicate_Emails()
        {
            var eventId = Guid.NewGuid();
            var eventData = new LeaveApprovedEto
            {
                EventId = eventId,
                LeaveRequestId = Guid.NewGuid(),
                ManagerResponse = "Test Onayı",
                ApproverId = Guid.NewGuid()
            };

            // 🔥 SENIOR FIX: 4. Parametre (IConfiguration) sahte (mock) olarak eklendi!
            var handler = new LeaveApprovedEventHandler(
                NullLogger<LeaveApprovedEventHandler>.Instance,
                _fakeEmailSender,
                _fakeInboxCache,
                Substitute.For<IConfiguration>()
            );

            await handler.HandleEventAsync(eventData);

            await _fakeEmailSender.Received(1).SendAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<AdditionalEmailSendingArgs>()
            );
        }
    }
}