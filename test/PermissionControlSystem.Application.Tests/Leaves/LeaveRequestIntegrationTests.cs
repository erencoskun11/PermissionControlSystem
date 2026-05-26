using Microsoft.Extensions.Configuration; // 🔥 YENİ EKLENDİ
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.EventHandlers.DistributedEvents;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Leaves.Dtos;
using Shouldly;
using System;
using System.Threading.Tasks;
using Volo.Abp.Emailing;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace PermissionControlSystem.Leave
{
    public class LeaveRequestIntegrationTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly ILeaveRequestAppService _leaveAppService;
        private readonly IRepository<LeaveRequest, Guid> _leaveRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public LeaveRequestIntegrationTests()
        {
            _leaveAppService = GetRequiredService<ILeaveRequestAppService>();
            _leaveRepository = GetRequiredService<IRepository<LeaveRequest, Guid>>();
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
            _employeeRepository = GetRequiredService<IEmployeeRepository>();
        }

        [Fact]
        public async Task Should_Approve_Leave_And_Trigger_Event()
        {
            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "Operasyon", ""), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Hasan", "Y", "h@test.com", "555", "Uzman"), autoSave: true);

            var leaveRequest = new LeaveRequest(Guid.NewGuid(), employee.Id, LeaveType.Annual, DateTime.Now.AddDays(1), DateTime.Now.AddDays(5), "Yıllık İzin");
            await _leaveRepository.InsertAsync(leaveRequest, autoSave: true);

            // 🔥 SENIOR FIX: Test ortamında da mührü (Stamp) DTO ile birlikte gönderiyoruz!
            // Eğer leaveRequest değişkeni bir Entity veya DTO ise, ConcurrencyStamp'i içinden alabilirsin.
            await _leaveAppService.ApproveAsync(leaveRequest.Id, new ApproveLeaveDto
            {
                ConcurrencyStamp = leaveRequest.ConcurrencyStamp
            });
            var updatedLeave = await _leaveRepository.GetAsync(leaveRequest.Id);
            updatedLeave.Status.ShouldBe(LeaveRequestStatus.Approved);

            var fakeEmailSender = Substitute.For<IEmailSender>();
            var fakeInboxCache = Substitute.For<IDistributedCache<string, string>>();

            // 🔥 SENIOR FIX: 4. Parametre (IConfiguration) sahte (mock) olarak eklendi!
            var eventHandler = new LeaveApprovedEventHandler(
                NullLogger<LeaveApprovedEventHandler>.Instance,
                fakeEmailSender,
                fakeInboxCache,
                Substitute.For<IConfiguration>()
            );

            var eto = new LeaveApprovedEto
            {
                EventId = Guid.NewGuid(),
                LeaveRequestId = leaveRequest.Id,
                ManagerResponse = "Test Onayı",
                ApproverId = Guid.NewGuid()
            };

            await eventHandler.HandleEventAsync(eto);
        }
    }
}