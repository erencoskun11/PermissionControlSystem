using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Events.LeaveRequest; // 🔥 Local eventler için eklendi
using PermissionControlSystem.Events.Leaves;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Leave.Dtos;
using PermissionControlSystem.Leaves.Strategies;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace PermissionControlSystem.Leaves
{
    public class LeaveRequestAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly ILeaveRequestRepository _leaveRepoMock;
        private readonly INotificationService _notificationMock;
        private readonly IElasticSearchService _elasticMock;
        private readonly IRepository<Employee, Guid> _employeeRepoMock;
        private readonly IClock _clockMock;
        private readonly ICurrentUser _currentUserMock;
        private readonly IDistributedCache<LeaveBalanceCacheItem, string> _leaveBalanceCacheMock;
        private readonly TestLeaveRequestAppService _service;

        private readonly IDistributedCache<LeaveRequestCacheItem, string> _singleLeaveCacheMock;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCacheMock;
        private readonly ILocalEventBus _localEventBusMock;

        public LeaveRequestAppService_Unit_Test()
        {
            _leaveRepoMock = Substitute.For<ILeaveRequestRepository>();
            _notificationMock = Substitute.For<INotificationService>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _employeeRepoMock = Substitute.For<IRepository<Employee, Guid>>();
            _clockMock = Substitute.For<IClock>();
            _currentUserMock = Substitute.For<ICurrentUser>();
            _leaveBalanceCacheMock = Substitute.For<IDistributedCache<LeaveBalanceCacheItem, string>>();
            _singleLeaveCacheMock = Substitute.For<IDistributedCache<LeaveRequestCacheItem, string>>();
            _employeeLeavesCacheMock = Substitute.For<IDistributedCache<List<LeaveRequestCacheItem>, string>>();
            _localEventBusMock = Substitute.For<ILocalEventBus>();

            var loggerMock = Substitute.For<Microsoft.Extensions.Logging.ILogger<LeaveRequestManager>>();

            var monday = new DateTime(2026, 2, 23, 10, 0, 0);
            _clockMock.Now.Returns(monday);

            var strategies = new List<ILeaveCalculationStrategy>();
            var mockStrategyFactory = new LeaveStrategyFactory(strategies);

            var manager = new LeaveRequestManager(
                _employeeRepoMock,
                _leaveRepoMock,
                _clockMock,
                _localEventBusMock,
                loggerMock,
                mockStrategyFactory
            );
            manager.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();

            // 🔥 SENIOR FIX: AppService'e tam olarak 9 parametre geçiriyoruz
            _service = new TestLeaveRequestAppService(
                _leaveRepoMock,
                _elasticMock,
                _employeeRepoMock,
                manager,
                _leaveBalanceCacheMock,
                _singleLeaveCacheMock,
                _employeeLeavesCacheMock,
                _localEventBusMock
            );

            _service.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();
        }

        [Fact]
        public async Task CreateAsync_Should_Create_Leave_And_Trigger_LocalEvent_When_Valid()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            var fakeEmployee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Eren", "Coskun", "eren@test.com", "123", "Dev");

            _employeeRepoMock.GetAsync(Arg.Any<Guid>())
                .ReturnsForAnyArgs(Task.FromResult(fakeEmployee));

            _leaveRepoMock.HasOverlappingLeaveAsync(empId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
                .Returns(false);

            var input = new CreateLeaveRequestDto
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Annual,
                StartDate = _clockMock.Now.AddDays(1),
                EndDate = _clockMock.Now.AddDays(2),
                Reason = "Yıllık İzin"
            };

            // 2. ACT 
            await _service.CreateAsync(input);

            // 3. ASSERT 
            await _leaveRepoMock.Received(1).InsertAsync(Arg.Any<LeaveRequest>(), true);

            // 🔥 Artık Outbox'ı değil, LocalEvent fırlatılmasını test ediyoruz!
            await _localEventBusMock.Received(1).PublishAsync(Arg.Is<LeaveRequestCreatedEvent>(e => e.EmployeeId == empId));
        }

        [Fact]
        public async Task UpdateAsync_Should_Update_Entity_And_AutoSave_To_Trigger_ABP_Event()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            var leaveId = Guid.NewGuid();
            var existingLeave = new LeaveRequest(leaveId, empId, LeaveType.Annual, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Eski Neden");

            _leaveRepoMock.GetAsync(leaveId).Returns(existingLeave);

            var input = new UpdateLeaveRequestDto
            {
                LeaveType = LeaveType.Unpaid,
                StartDate = _clockMock.Now.AddDays(5),
                EndDate = _clockMock.Now.AddDays(6),
                Reason = "Yeni Neden"
            };

            // 2. ACT
            await _service.UpdateAsync(leaveId, input);

            // 3. ASSERT
            await _leaveRepoMock.Received(1).UpdateAsync(Arg.Is<LeaveRequest>(l => l.Reason == "Yeni Neden"), true);
        }

        [Fact]
        public async Task ApproveAsync_Should_Approve_Leave_And_Update_Database()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            var leaveId = Guid.NewGuid();
            var leave = new LeaveRequest(leaveId, empId, LeaveType.Annual, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Tatil");

            _leaveRepoMock.GetAsync(leaveId).Returns(leave);

            // 2. ACT
            await _service.ApproveAsync(leaveId);

            // 3. ASSERT
            leave.Status.ShouldBe(LeaveRequestStatus.Approved);
            await _leaveRepoMock.Received(1).UpdateAsync(leave, true);
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_From_Database()
        {
            var leaveId = Guid.NewGuid();

            await _service.DeleteAsync(leaveId);

            await _leaveRepoMock.Received(1).DeleteAsync(leaveId, Arg.Any<bool>());
        }

        [Fact]
        public async Task BulkCreateAsync_Should_Insert_Many_And_Publish_LocalEvent()
        {
            var empId1 = Guid.NewGuid();
            var empId2 = Guid.NewGuid();

            var input = new List<CreateLeaveRequestDto>
            {
                new CreateLeaveRequestDto { EmployeeId = empId1, LeaveType = LeaveType.Annual, StartDate = _clockMock.Now.AddDays(1), EndDate = _clockMock.Now.AddDays(2), Reason = "A" },
                new CreateLeaveRequestDto { EmployeeId = empId2, LeaveType = LeaveType.Annual, StartDate = _clockMock.Now.AddDays(1), EndDate = _clockMock.Now.AddDays(2), Reason = "B" }
            };

            await _service.BulkCreateAsync(input);

            // Veritabanına toplu kayıt
            await _leaveRepoMock.Received(1).InsertManyAsync(
                Arg.Is<IEnumerable<LeaveRequest>>(x => System.Linq.Enumerable.Count(x) == 2),
                true
            );

            // LocalEventBus fırlatıldı mı?
            await _localEventBusMock.Received(1).PublishAsync(
                Arg.Is<LeaveRequestsBulkCreatedEvent>(x => System.Linq.Enumerable.Count(x.LeaveRequests) == 2)
            );
        }

        [Fact]
        public async Task AutoRejectExpiredLeavesAsync_Should_Work_Correctly()
        {
            var oldLeave = new LeaveRequest(Guid.NewGuid(), Guid.NewGuid(), LeaveType.Annual, DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-5), "Old");
            var list = new List<LeaveRequest> { oldLeave };

            _leaveRepoMock.GetListAsync(Arg.Any<Expression<Func<LeaveRequest, bool>>>(), false, default)
                          .ReturnsForAnyArgs(Task.FromResult(list));

            await _service.AutoRejectExpiredLeavesAsync();

            oldLeave.Status.ShouldBe(LeaveRequestStatus.Rejected);
        }
    }
}