using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Events;
using Volo.Abp.Emailing;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using PermissionControlSystem.SignalR;

namespace PermissionControlSystem.EventHandlers.Tests
{
    public class LeaveRequestCreatedEventHandlerTests : PermissionControlSystemDomainTestBase<PermissionControlSystemEventHandlersTestModule>
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

       
    }
}