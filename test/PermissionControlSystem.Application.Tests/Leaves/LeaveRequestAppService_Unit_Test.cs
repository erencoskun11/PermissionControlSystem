using Microsoft.Extensions.Caching.Distributed; // 🔥 Cache için eklendi
using NSubstitute;
using PermissionControlSystem.Caching; // 🔥 LeaveBalanceCacheItem için eklendi
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Leave.Dtos;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus.Distributed;
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
        private readonly IDistributedEventBus _eventBusMock;
        private readonly IClock _clockMock;
        private readonly IEmailSender _fakeEmailSender;
        private readonly ICurrentUser _currentUserMock;
        private readonly IDistributedCache<LeaveBalanceCacheItem, string> _leaveBalanceCacheMock; // 🔥 Yeni Cache Kalkanımız!
        private readonly LeaveRequestAppService _service;

        private readonly IDistributedCache<LeaveRequestCacheItem, string> _singleLeaveCacheMock;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCacheMock;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepoMock; // 🔥 Outbox Repository Mock'u

        private readonly ILocalEventBus _localEventBusMock;

        public LeaveRequestAppService_Unit_Test()
        {
            _fakeEmailSender = Substitute.For<IEmailSender>();
            _leaveRepoMock = Substitute.For<ILeaveRequestRepository>();
            _notificationMock = Substitute.For<INotificationService>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _employeeRepoMock = Substitute.For<IRepository<Employee, Guid>>();
            _eventBusMock = Substitute.For<IDistributedEventBus>();
            _clockMock = Substitute.For<IClock>();
            _currentUserMock = Substitute.For<ICurrentUser>();
            _outboxRepoMock = Substitute.For<IRepository<OutboxMessage, Guid>>();
            _leaveBalanceCacheMock = Substitute.For<IDistributedCache<LeaveBalanceCacheItem, string>>();
            _singleLeaveCacheMock = Substitute.For<IDistributedCache<LeaveRequestCacheItem, string>>();
            _employeeLeavesCacheMock = Substitute.For<IDistributedCache<List<LeaveRequestCacheItem>, string>>();
            _localEventBusMock = Substitute.For<ILocalEventBus>(); // ✅ LocalEventBus added

            // ✅ We also need a fake Logger for the Manager now!
            var loggerMock = Substitute.For<Microsoft.Extensions.Logging.ILogger<LeaveRequestManager>>();

            var monday = new DateTime(2026, 2, 23, 10, 0, 0);
            _clockMock.Now.Returns(monday);

            // ✅ Pass 5 parameters to the Manager (including localEventBus and logger)
            var manager = new LeaveRequestManager(
                _employeeRepoMock,
                _leaveRepoMock,
                _clockMock,
                _localEventBusMock,
                loggerMock
            );
            manager.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();

            // ✅ Pass 11 parameters to the AppService (including localEventBus)
            _service = new LeaveRequestAppService(
                _leaveRepoMock,
                _notificationMock,
                _elasticMock,
                _employeeRepoMock,
                _eventBusMock,
                manager,
                _leaveBalanceCacheMock,
                _singleLeaveCacheMock,
                _employeeLeavesCacheMock,
                _outboxRepoMock,
                _localEventBusMock // 🔥 11th parameter
            );

            _service.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();
        }

        [Fact]
        public async Task CreateAsync_Should_Create_Leave_And_Trigger_All_Services_When_Valid()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();

            var fakeEmployee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Eren", "Coskun", "eren@test.com", "123", "Dev");

            _employeeRepoMock.GetAsync(Arg.Any<Guid>())
                             .ReturnsForAnyArgs(Task.FromResult(fakeEmployee));

            _employeeRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .ReturnsForAnyArgs(Task.FromResult(fakeEmployee));

            var emptyLeaveList = new List<LeaveRequest>();
            _leaveRepoMock.GetListAsync(Arg.Any<Expression<Func<LeaveRequest, bool>>>())
                          .ReturnsForAnyArgs(Task.FromResult(emptyLeaveList));

            _leaveRepoMock.HasOverlappingLeaveAsync(empId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
                          .Returns(false);

            var input = new CreateLeaveRequestDto
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Annual,
                StartDate = _clockMock.Now.AddDays(1),
                EndDate = _clockMock.Now.AddDays(2),
                Reason = "Test"
            };

            // 2. ACT 
            await _service.CreateAsync(input);

            // 3. ASSERT 
            await _leaveRepoMock.Received(1).InsertAsync(Arg.Any<LeaveRequest>(), true);
        }

        [Fact]
        public async Task AutoRejectExpiredLeavesAsync_Should_Work_Correctly()
        {
            var oldLeave = new LeaveRequest(Guid.NewGuid(), Guid.NewGuid(), LeaveType.Annual, DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-5), "Old");
            var list = new List<LeaveRequest> { oldLeave };

            _leaveRepoMock.GetListAsync(Arg.Any<Expression<Func<LeaveRequest, bool>>>())
                          .ReturnsForAnyArgs(Task.FromResult(list));

            await _service.AutoRejectExpiredLeavesAsync();

            oldLeave.Status.ShouldBe(LeaveRequestStatus.Rejected);
        }

        [Fact]
        public async Task CreateAsync_Should_Create_Leave_Trigger_Outbox_And_Clear_Cache()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            var fakeEmployee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Eren", "Coskun", "eren@test.com", "123", "Dev");

            _employeeRepoMock.GetAsync(Arg.Any<Guid>())
                .ReturnsForAnyArgs(Task.FromResult(fakeEmployee));

            _employeeRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Employee,bool>>>())
                .ReturnsForAnyArgs(Task.FromResult(fakeEmployee));

            _leaveRepoMock.GetListAsync(Arg.Any<Expression<Func<LeaveRequest, bool>>>(), false, default).Returns(new List<LeaveRequest>());
            _leaveRepoMock.HasOverlappingLeaveAsync(empId, Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(false);

            var input = new CreateLeaveRequestDto
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Annual,
                StartDate = _clockMock.Now.AddDays(1),
                EndDate = _clockMock.Now.AddDays(2),
                Reason = "Yıllık İzin"
            };

            // 2. ACT 
            var result = await _service.CreateAsync(input);

            // 3. ASSERT 
            result.ShouldNotBeNull();

            // Veritabanına Insert edildi mi?
            await _leaveRepoMock.Received(1).InsertAsync(Arg.Any<LeaveRequest>(), true);

            // 🔥 SENIOR FIX: Outbox tablosuna LeaveRequestCreated mesajı atıldı mı?
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "LeaveRequestCreated"));

            // Bildirim ve RabbitMQ tetiklendi mi?
            await _notificationMock.Received(1).AddNotificationAsync(Arg.Any<string>());
            await _eventBusMock.Received(1).PublishAsync(Arg.Is<LeaveRequestCreatedEto>(e => e.EmployeeName == "Eren Coskun"));

            // 🔥 SENIOR FIX: Personelin liste ve bakiye cache'leri temizlendi mi?
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId}", null, false, default);
            await _leaveBalanceCacheMock.Received(1).RemoveAsync($"leave_balance_{empId}", null, false, default);

        }


        [Fact]
        public async Task UpdateAsync_Should_Update_Trigger_Outbox_And_Clear_Caches()
        {
            var empId = Guid.NewGuid();
            var leaveId = Guid.NewGuid();
            var existingLeave = new LeaveRequest(leaveId, empId, LeaveType.Annual, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Eski Neden");
            var fakeEmployee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Ali", "Veli", "ali@test.com", "111", "HR");


            _leaveRepoMock.GetAsync(leaveId).Returns(existingLeave);
            _employeeRepoMock.GetAsync(empId).Returns(fakeEmployee);

            var input = new UpdateLeaveRequestDto
            {
                LeaveType = LeaveType.Unpaid,
                StartDate = _clockMock.Now.AddDays(5),
                EndDate = _clockMock.Now.AddDays(6),
                Reason = "Yeni Neden"
            };

            await _service.UpdateAsync(leaveId, input);


            // Veritabanı güncellendi mi?
            await _leaveRepoMock.Received(1).UpdateAsync(Arg.Is<LeaveRequest>(l=>l.Reason=="Yeni Neden"), true);

            // 🔥 SENIOR FIX: Outbox tablosuna LeaveRequestUpdated mesajı atıldı mı?
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o=>o.Type == "LeaveRequestUpdated"));

            // Tüm ilgili Cache'ler temizlendi mi?
            await _singleLeaveCacheMock.Received(1).RemoveAsync($"LeaveRequest_{leaveId}", null, false, default);
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId}", null, false, default);
            await _leaveBalanceCacheMock.Received(1).RemoveAsync($"leave_balance_{empId}", null, false, default);

        }

        [Fact]
        public async Task ApproveAsync_Should_Approve_Leave_And_Update_Outbox()
        {
            var empId = Guid.NewGuid();
            var leaveId = Guid.NewGuid();
            var leave = new LeaveRequest(leaveId, empId, LeaveType.Annual, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Tatil");
            var employee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Can", "Kan", "can@test.com", "1", "IT");

            _leaveRepoMock.GetAsync(leaveId).Returns(leave);
            _employeeRepoMock.GetAsync(empId).Returns(employee);

            await _service.ApproveAsync(leaveId);

            leave.Status.ShouldBe(LeaveRequestStatus.Approved);
            await _leaveRepoMock.Received(1).UpdateAsync(leave, true);

            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "LeaveRequestUpdated"));

            await _singleLeaveCacheMock.Received(1).RemoveAsync($"LeaveRequest_{leaveId}", null, false, default);
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_Leave_And_Add_Outbox_Message()
        {
            var empId = Guid.NewGuid();
            var leaveId = Guid.NewGuid();
            var leave = new LeaveRequest(leaveId, empId, LeaveType.Annual, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Tatil");

            _leaveRepoMock.GetAsync(leaveId).Returns(leave);

            await _service.DeleteAsync(leaveId);

            await _leaveRepoMock.Received(1).DeleteAsync(leaveId, Arg.Any<bool>());
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "LeaveRequestDeleted"));

            // DeleteAsync içine eklediğimiz Cache silme kuralları
            await _singleLeaveCacheMock.Received(1).RemoveAsync($"LeaveRequest_{leaveId}", null, false, default);
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId}", null, false, default);
            await _leaveBalanceCacheMock.Received(1).RemoveAsync($"leave_balance_{empId}", null, false, default);
        }

        [Fact]
        public async Task BulkCreateAsync_Should_Insert_Many_And_Outbox()
        {
            var empId1 = Guid.NewGuid();
            var empId2 = Guid.NewGuid();

            var input = new List<CreateLeaveRequestDto>
            {
                new CreateLeaveRequestDto { EmployeeId = empId1, LeaveType = LeaveType.Annual, StartDate = _clockMock.Now.AddDays(1), EndDate = _clockMock.Now.AddDays(2), Reason = "A" },
                new CreateLeaveRequestDto { EmployeeId = empId2, LeaveType = LeaveType.Annual, StartDate = _clockMock.Now.AddDays(1), EndDate = _clockMock.Now.AddDays(2), Reason = "B" }
            };

            await _service.BulkCreateAsync(input);

            // 🔥 SENIOR FIX: Derleyici hatalarını (CS1501, CS1061) aşmak için Enumerable sınıfını doğrudan kullanıyoruz.
            await _leaveRepoMock.Received(1).InsertManyAsync(
                Arg.Is<IEnumerable<LeaveRequest>>(x => System.Linq.Enumerable.Count(x) == 2),
                true
            );

            // 🔥 SENIOR FIX: autoSave parametresini Arg.Any<bool>() yaparak olası parametre uyuşmazlıklarını da engelliyoruz.
            await _outboxRepoMock.Received(1).InsertManyAsync(
                Arg.Is<IEnumerable<OutboxMessage>>(x =>
                    System.Linq.Enumerable.Count(x) == 2 &&
                    System.Linq.Enumerable.First(x).Type == "LeaveRequestCreated"),
                Arg.Any<bool>()
            );

            // Bulk işlem sonrası her bir personelin cache'inin ayrı ayrı temizlendiğini denetliyoruz
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId1}", null, false, default);
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId2}", null, false, default);
        }


        [Fact]
        public async Task AutoRejectExpiredLeavesAsync_Should_Work_Correctly_And_Clear_Cache()
        {
            var empId = Guid.NewGuid();
            var oldLeave = new LeaveRequest(Guid.NewGuid(), empId, LeaveType.Annual, _clockMock.Now.AddDays(-10), _clockMock.Now.AddDays(-5), "Old");
            var list = new List<LeaveRequest> { oldLeave };

            _leaveRepoMock.GetListAsync(Arg.Any<Expression<Func<LeaveRequest, bool>>>(), false, default).Returns(list);

            await _service.AutoRejectExpiredLeavesAsync();

            oldLeave.Status.ShouldBe(LeaveRequestStatus.Rejected);

            // Eğer bir izin reddedildiyse, o personelin cache'i mutlaka temizlenmeli!
            await _employeeLeavesCacheMock.Received(1).RemoveAsync($"EmployeeLeaves_{empId}", null, false, default);
        }

    }
}