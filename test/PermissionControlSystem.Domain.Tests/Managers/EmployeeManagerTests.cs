using PermissionControlSystem.Entities;
using PermissionControlSystem.Managers;
using Shouldly;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace PermissionControlSystem.Employees
{
    [Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
    public class EmployeeManagerTests : PermissionControlSystemDomainTestBase<PermissionControlSystemDomainTestModule>
    {
        private readonly EmployeeManager _employeeManager;
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IRepository<Department, Guid> _departmentRepository;

        public EmployeeManagerTests()
        {
            _employeeManager = GetRequiredService<EmployeeManager>();
            _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            _departmentRepository = GetRequiredService<IRepository<Department, Guid>>();
        }

        [Fact]
        public async Task Should_Throw_Exception_If_Email_Already_Exists()
        {
            var departmentId = Guid.NewGuid();
            var uniqueDeptName = "Dept_EmpTest_1"; // 🔥 Çakışma önleyici benzersiz isim
            var duplicateEmail = "test@sirket.com";

            await WithUnitOfWorkAsync(async () =>
            {
                var department = await _departmentRepository.InsertAsync(new Department(departmentId, uniqueDeptName, "Desc"), autoSave: true);

                await _employeeRepository.InsertAsync(new Employee(
                    Guid.NewGuid(), Guid.NewGuid(), department.Id, "A", "B", duplicateEmail, "1", "C"
                ), autoSave: true);
            });

            var exception = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    await _employeeManager.CreateAsync(
                        Guid.NewGuid(), departmentId, "Yeni", "Personel", duplicateEmail, "555", "Uzman"
                    );
                });
            });

            exception.Code.ShouldBe("Emp:001");
        }

        [Fact] // 🔥 BU EKSİKTİ, TEST ÇALIŞMIYORDU!
        public async Task ChangeEmailAsync_Should_Throw_Exception_If_Email_Taken_By_Another_Person()
        {
            var deptId = Guid.NewGuid();
            var uniqueDeptName = "Dept_EmpTest_2"; // 🔥 Çakışma önleyici benzersiz isim

            var emp1Id = Guid.NewGuid();
            var emp2Id = Guid.NewGuid();
            var targetEmail = "eren@test.com"; // Emp1'in maili, Emp2 bunu almaya çalışacak

            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(new Department(deptId, uniqueDeptName, "D"), autoSave: true);

                await _employeeRepository.InsertAsync(new Employee(emp1Id, Guid.NewGuid(), deptId, "Eren", "C", targetEmail, "1", "Dev"), autoSave: true);
                await _employeeRepository.InsertAsync(new Employee(emp2Id, Guid.NewGuid(), deptId, "Ali", "V", "ali@test.com", "2", "QA"), autoSave: true);
            });

            // ACT & ASSERT: Ali'nin mailini Eren'in mailiyle aynı yapmaya çalışalım
            await Should.ThrowAsync<BusinessException>(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    // EF Core detached hatası almamak için nesneyi taze çekiyoruz
                    var emp2 = await _employeeRepository.GetAsync(emp2Id);
                    await _employeeManager.ChangeEmailAsync(emp2, targetEmail);
                });
            });
        }

        [Fact]
        public async Task Should_Create_Employee_If_Email_Is_Unique()
        {
            var departmentId = Guid.NewGuid();
            var uniqueDeptName = "Dept_EmpTest_3"; // 🔥 Çakışma önleyici benzersiz isim

            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(new Department(departmentId, uniqueDeptName, "Desc"), autoSave: true);
            });

            Employee result = null;

            await WithUnitOfWorkAsync(async () =>
            {
                result = await _employeeManager.CreateAsync(
                    Guid.NewGuid(), departmentId, "Ahmet", "Yılmaz", "ahmet_unique@sirket.com", "555", "Uzman"
                );
            });

            result.ShouldNotBeNull();
            result.Email.ShouldBe("ahmet_unique@sirket.com");
        }
    }
}