using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.Departments
{
    public class DepartmentRepositoryTests : PermissionControlSystemEntityFrameworkCoreTestBase
    {
        private readonly IDepartmentRepository _departmentRepository;

        public DepartmentRepositoryTests()
        {
            _departmentRepository = GetRequiredService<IDepartmentRepository>();
        }

        [Fact]
        public async Task FindByNameAsync_Should_Return_Correct_Department()
        {
            var deptName = "Test_IT_Department";
            await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), deptName, "Desc"), autoSave: true);

            var result = await _departmentRepository.FindByNameAsync(deptName);

            result.ShouldNotBeNull();
            result.Name.ShouldBe(deptName);
        }

        [Fact]
        public async Task GetListAsync_Should_Filter_By_Name()
        {
            await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "Muhasebe", "Desc"), autoSave: true);
            await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), "Mühendislik", "Desc"), autoSave: true);

            var result = await _departmentRepository.GetListAsync(filter: "Muh");

            result.Count.ShouldBe(1);
            result.First().Name.ShouldBe("Muhasebe");
        }
    }
}