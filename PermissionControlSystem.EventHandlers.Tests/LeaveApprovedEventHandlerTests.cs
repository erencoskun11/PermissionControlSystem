using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace PermissionControlSystem.EventHandlers
{
    public class LeaveApprovedEventHandlerTests : PermissionControlSystemDomainTestBase<PermissionControlSystemEventHandlersTestModule>
    {
        private readonly LeaveApprovedEventHandler _handler;
        private readonly IEmailSender _fakeEmailSender;
        private readonly IRepository<IncomingMessage, Guid> _fakeIncomingMessageRepository;

        public LeaveApprovedEventHandlerTests()
        {
            _fakeEmailSender = Substitute.For<IEmailSender>();

            _fakeIncomingMessageRepository = Substitute.For<IRepository<IncomingMessage, Guid>>();

            _handler = new LeaveApprovedEventHandler(
                NullLogger<LeaveApprovedEventHandler>.Instance,
                _fakeEmailSender,
                _fakeIncomingMessageRepository
            );
        }

        [Fact]
        public async Task HandleEventAsync_Should_Send_Email_And_Save_To_Inbox_If_New_Message()
        {
            var eventData = new LeaveApprovedEto
            {
                EventId = Guid.NewGuid(),
                LeaveRequestId = Guid.NewGuid(),
                ManagerResponse = "İyi tatiller"
            };

            _fakeIncomingMessageRepository.AnyAsync(Arg.Any<Expression<Func<IncomingMessage, bool>>>())
                .Returns(Task.FromResult(false));

            await _handler.HandleEventAsync(eventData);

            await _fakeEmailSender.Received(1).SendAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains("İyi tatiller"))
            );

            await _fakeIncomingMessageRepository.Received(1).InsertAsync(
                Arg.Is<IncomingMessage>(x => x.EventId == eventData.EventId),
                autoSave: true
            );
        }

        [Fact]
        public async Task HandleEventAsync_Should_Skip_If_Message_Already_Processed()
        {
            var eventId = Guid.NewGuid();
            var eventData = new LeaveApprovedEto { EventId = eventId, LeaveRequestId = Guid.NewGuid() };

            _fakeIncomingMessageRepository.AnyAsync(Arg.Any<Expression<Func<IncomingMessage, bool>>>())
                .Returns(Task.FromResult(true));

            await _handler.HandleEventAsync(eventData);

            await _fakeEmailSender.Received(0).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}