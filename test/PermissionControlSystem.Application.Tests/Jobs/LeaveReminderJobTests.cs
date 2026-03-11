using Microsoft.Extensions.Logging;
using NSubstitute;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events.LeaveRequest;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Managers;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Timing;
using Xunit;

namespace PermissionControlSystem.Jobs
{
    public class LeaveReminderJobTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IRepository<LeaveRequest, Guid> _leaveRequestRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeeRepository _employeeRepository;

        private readonly ILocalEventBus _localEventBusMock;
        private readonly ILogger<LeaveRequestManager> _loggerMock;

        private readonly LeaveRequestManager _manager;
        private readonly IClock _clockMock;

        public LeaveReminderJobTests()
        {
            _leaveRequestRepository = GetRequiredService<IRepository<LeaveRequest, Guid>>();
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
            _employeeRepository = GetRequiredService<IEmployeeRepository>();

            // 🔥 Mocking the new dependencies
            _localEventBusMock = Substitute.For<ILocalEventBus>();
            _loggerMock = Substitute.For<ILogger<LeaveRequestManager>>();
            _clockMock = Substitute.For<IClock>();

            _clockMock.Now.Returns(new DateTime(2026, 03, 05));

            // 🔥 Passing the correct 5 parameters to the new Manager
            _manager = new LeaveRequestManager(
                _employeeRepository,
                _leaveRequestRepository,
                _clockMock,
                _localEventBusMock, // 4th: EventBus instead of EmailSender
                _loggerMock         // 5th: Logger instead of CurrentUser
            );
        }

        [Fact]
        public async Task Should_Publish_Event_If_Overdue_Leaves_Exist()
        {
            // ARRANGE
            _clockMock.Now.Returns(new DateTime(2026, 03, 05));

            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "TestDept", ""), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Ahmet", "Yılmaz", "a@test.com", "123", "Uzman"), autoSave: true);

            var overdueLeave = new LeaveRequest(
                Guid.NewGuid(), employee.Id, LeaveType.Annual, _clockMock.Now.AddDays(5), _clockMock.Now.AddDays(10), "Test"
            );

            // Making the leave 31 days old
            var creationTimeProperty = typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.CreationTime));
            creationTimeProperty?.SetValue(overdueLeave, _clockMock.Now.AddDays(-31));

            await _leaveRequestRepository.InsertAsync(overdueLeave, autoSave: true);

            var job = new LeaveReminderJob(_manager);

            // ACT
            await job.CheckOldLeavesAsync();

            // ASSERT
            // 🔥 We don't check emails anymore; we check if the Event was published!
            await _localEventBusMock.Received(1).PublishAsync(Arg.Any<LeaveReminderNeededEvent>());
        }


        [Fact]
        public async Task Should_Not_Publish_Event_If_No_Overdue_Leaves()
        {
            // ARRANGE
            _clockMock.Now.Returns(new DateTime(2026, 03, 05));

            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "TestDept2", ""), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Mehmet", "Kaya", "m@test.com", "123", "Uzman"), autoSave: true);

            var freshLeave = new LeaveRequest(
                Guid.NewGuid(), employee.Id, LeaveType.Unpaid, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Taze İzin"
            );

            await _leaveRequestRepository.InsertAsync(freshLeave, autoSave: true);

            var job = new LeaveReminderJob(_manager);

            // ACT
            await job.CheckOldLeavesAsync();

            // ASSERT
            // 🔥 No events should be published
            await _localEventBusMock.Received(0).PublishAsync(Arg.Any<LeaveReminderNeededEvent>());
        }
    }
}