using System;
using System.Threading.Tasks;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using Shouldly;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.Employees
{
    [Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
    public class EmployeeRepositoryTests : PermissionControlSystemEntityFrameworkCoreTestBase
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDepartmentRepository _departmentRepository;

        public EmployeeRepositoryTests()
        {
            _employeeRepository = GetRequiredService<IEmployeeRepository>();
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
        }

        [Fact]
        public async Task GetWithDetailsAsync_Should_Include_Department()
        {
            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "IK", "Desc"), autoSave: true);
            var employee = await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Ali", "Veli", "a@b.com", "123", "Uzman"), autoSave: true);

            var result = await _employeeRepository.GetWithDetailsAsync(employee.Id);

            result.ShouldNotBeNull();
            result.Department.ShouldNotBeNull();
            result.Department.Name.ShouldBe("IK");
        }

        [Fact]
        public async Task GetCountAsync_Should_Return_Correct_Count_With_Filter()
        {
            var department = await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "ArGe", "Desc"), autoSave: true);

            await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Ahmet", "Kaya", "1@b.com", "1", "A"), autoSave: true);
            await _employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), Guid.NewGuid(), department.Id, "Mehmet", "Kaya", "2@b.com", "2", "B"), autoSave: true);

            var count = await _employeeRepository.GetCountAsync(filter: "Ahmet");

            count.ShouldBe(1);
        }
    }
}