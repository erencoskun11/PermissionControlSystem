using PermissionControlSystem.EventHandlers;
using PermissionControlSystem.Events;
using PermissionControlSystem.Leaves2;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace PermissionControlSystem.Leaves
{
    public class LeaveRequestIntegrationTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly ILeaveRequestAppService _leaveAppService;
        private readonly IRepository<LeaveRequest, Guid> _leaveRepository;
        private readonly LeaveApprovedEventHandler _eventHandler;


        public LeaveRequestIntegrationTests()
        {
            _leaveAppService = GetRequiredService<ILeaveRequestAppService>();
            _leaveRepository = GetRequiredService<IRepository<LeaveRequest, Guid>>();
            _eventHandler = GetRequiredService<LeaveApprovedEventHandler>();
        }

        [Fact]
        public async Task Should_Approve_Leave_And_Trigger_Event()
        {
            //Arrange
            var leaveRequest = (await _leaveRepository.GetListAsync()).FirstOrDefault();

            if (leaveRequest == null)
            {
                return;
            }

            //Act
            await _leaveAppService.ApproveAsync(leaveRequest.Id);

            //Assert 
            var updateLeave = await _leaveRepository.GetAsync(leaveRequest.Id);
            updateLeave.Status.ShouldBe(LeaveRequestStatus.Approved);

            var eto = new LeaveApprovedEto
            {
                LeaveRequestId = leaveRequest.Id,
                ManagerResponse = "Test Onayı"
            };

            await _eventHandler.HandleEventAsync(eto);
        }

     }
}
