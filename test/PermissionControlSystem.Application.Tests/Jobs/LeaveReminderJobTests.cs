using NSubstitute;
using PermissionControlSystem.Departments;
using PermissionControlSystem.Employees;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Jobs;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Managers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace PermissionControlSystem.Jobs
{
    public class LeaveReminderJobTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IRepository<LeaveRequest, Guid> _leaveRequestRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IEmailSender _fakeEmailSender;
        private readonly LeaveRequestManager _manager;

        // 🔥 KRİTİK DEĞİŞİKLİK: Saat artık test metotlarından erişilebilir bir field!
        private readonly IClock _clockMock;

        public LeaveReminderJobTests()
        {
            _leaveRequestRepository = GetRequiredService<IRepository<LeaveRequest, Guid>>();
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
            _employeeRepository = GetRequiredService<IEmployeeRepository>();
            _fakeEmailSender = Substitute.For<IEmailSender>();

            // 🔥 Yerel değişken değil, field olarak tanımlıyoruz
            _clockMock = Substitute.For<IClock>();

            // 🔥 Varsayılan olarak bugünü veriyoruz ki taşma (overflow) olmasın
            _clockMock.Now.Returns(new DateTime(2026, 03, 05));

            var currentUser = Substitute.For<ICurrentUser>();

            _manager = new LeaveRequestManager(
                _employeeRepository,
                _leaveRequestRepository,
                _clockMock, // Artık field olan clock'u veriyoruz
                _fakeEmailSender,
                currentUser);
        }

        [Fact]
        public async Task Should_Send_Email_If_Overdue_Leaves_Exist()
        {
            // ARRANGE
            _clockMock.Now.Returns(new DateTime(2026, 03, 05)); // Güvenli bir tarih

            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "TestDept", ""), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Ahmet", "Yılmaz", "a@test.com", "123", "Uzman"), autoSave: true);

            var overdueLeave = new LeaveRequest(
                Guid.NewGuid(), employee.Id, LeaveType.Annual, _clockMock.Now.AddDays(5), _clockMock.Now.AddDays(10), "Test"
            );

            // 30 günden daha eski bir tarih veriyoruz ki manager bunu bulsun
            var creationTimeProperty = typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.CreationTime));
            creationTimeProperty?.SetValue(overdueLeave, _clockMock.Now.AddDays(-31));

            await _leaveRequestRepository.InsertAsync(overdueLeave, autoSave: true);

            var job = new LeaveReminderJob(_manager);

            // ACT
            await job.CheckOldLeavesAsync();

            // ASSERT
            await _fakeEmailSender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task Should_Not_Send_Email_If_No_Overdue_Leaves()
        {
            // ARRANGE
            // 🔥 İşte hatayı çözen satır: Saati 2026'ya sabitliyoruz!
            _clockMock.Now.Returns(new DateTime(2026, 03, 05));

            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "TestDept2", ""), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Mehmet", "Kaya", "m@test.com", "123", "Uzman"), autoSave: true);

            var freshLeave = new LeaveRequest(
                Guid.NewGuid(), employee.Id, LeaveType.Unpaid, _clockMock.Now.AddDays(1), _clockMock.Now.AddDays(2), "Taze İzin"
            );

            // Bu izin taze olduğu için (CreationTime = Now) manager bunu bulmamalı
            await _leaveRequestRepository.InsertAsync(freshLeave, autoSave: true);

            var job = new LeaveReminderJob(_manager);

            // ACT
            await job.CheckOldLeavesAsync();

            // ASSERT
            await _fakeEmailSender.Received(0).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}