using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Leaves;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.Leaves
{
    // 🔥 TRAFİK POLİSİ: Diğer testlerle çakışıp veritabanını kilitlemesini engeller
    [Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
    public class LeaveRequestRepositoryTests : PermissionControlSystemEntityFrameworkCoreTestBase
    {
        private readonly ILeaveRequestRepository _leaveRequestRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDepartmentRepository _departmentRepository;

        public LeaveRequestRepositoryTests()
        {
            _leaveRequestRepository = GetRequiredService<ILeaveRequestRepository>();
            _employeeRepository = GetRequiredService<IEmployeeRepository>();
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
        }

        [Fact]
        public async Task HasOverlappingLeaveAsync_Should_Detect_Overlaps()
        {
            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "Dep", "Desc"), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "A", "B", "a@b.com", "1", "C"), autoSave: true);

            var existingLeave = new LeaveRequest(Guid.NewGuid(), employee.Id, LeaveType.Annual, new DateTime(2026, 5, 10), new DateTime(2026, 5, 15), "Tatil");

            // 🔥 SENIOR DOKUNUŞU: Dışarıdan atama yasak! Zeki metodu kullanıyoruz.
            existingLeave.Approve("Yönetici onayı");

            await _leaveRequestRepository.InsertAsync(existingLeave, autoSave: true);

            var hasOverlap = await _leaveRequestRepository.HasOverlappingLeaveAsync(employee.Id, new DateTime(2026, 5, 12), new DateTime(2026, 5, 20));
            var hasNoOverlap = await _leaveRequestRepository.HasOverlappingLeaveAsync(employee.Id, new DateTime(2026, 5, 20), new DateTime(2026, 5, 25));

            hasOverlap.ShouldBeTrue();
            hasNoOverlap.ShouldBeFalse();
        }

        [Fact]
        public async Task GetUsedDaysYearlyAsync_Should_Calculate_Total_Days_Correctly()
        {
            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "Dep2", "Desc"), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "C", "D", "c@d.com", "2", "E"), autoSave: true);

            var leave1 = new LeaveRequest(Guid.NewGuid(), employee.Id, LeaveType.Annual, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12), "Kış");
            leave1.Approve("Kış tatili onaylandı"); // 🔥 Zeki metot kullanıldı
            await _leaveRequestRepository.InsertAsync(leave1, autoSave: true);

            var leave2 = new LeaveRequest(Guid.NewGuid(), employee.Id, LeaveType.Annual, new DateTime(2026, 8, 1), new DateTime(2026, 8, 5), "Yaz");
            leave2.Approve("Yaz tatili onaylandı"); // 🔥 Zeki metot kullanıldı
            await _leaveRequestRepository.InsertAsync(leave2, autoSave: true);

            var leave3 = new LeaveRequest(Guid.NewGuid(), employee.Id, LeaveType.Annual, new DateTime(2026, 12, 1), new DateTime(2026, 12, 5), "Reddedilen");
            leave3.Reject("İş yoğunluğu sebebiyle reddedildi"); // 🔥 Zeki metot kullanıldı
            await _leaveRequestRepository.InsertAsync(leave3, autoSave: true);

            var totalDays = await _leaveRequestRepository.GetUsedDaysYearlyAsync(employee.Id, 2026);

            totalDays.ShouldBe(8);
        }
    }
}