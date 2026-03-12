using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Entities;
using PermissionControlSystem.EventHandlers.DistributedEvents;
using PermissionControlSystem.Events;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace PermissionControlSystem.EventHandlers
{
    public class LeaveApprovedEventHandlerTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IEmailSender _fakeEmailSender;
        private readonly IRepository<IncomingMessage, Guid> _fakeRepo;

        public LeaveApprovedEventHandlerTests()
        {
            _fakeEmailSender = Substitute.For<IEmailSender>();
            _fakeRepo = Substitute.For<IRepository<IncomingMessage, Guid>>();
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

            var handler = new LeaveApprovedEventHandler(NullLogger<LeaveApprovedEventHandler>.Instance, _fakeEmailSender, _fakeRepo);

            await handler.HandleEventAsync(eventData);

            await _fakeEmailSender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}