using PermissionControlSystem.Departments.Dtos;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppServiceTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IDepartmentAppService _departmentAppService;

        public DepartmentAppServiceTests()
        {
            _departmentAppService = GetRequiredService<IDepartmentAppService>();
        }

        [Fact]
        public async Task Should_Get_List_Of_Departments()
        {
            //Act
            var result = await _departmentAppService.GetListAsync(
            new PagedAndSortedResultRequestDto());

            //Assert
            result.TotalCount.ShouldBeGreaterThan(0);
            result.Items.ShouldContain(d => d.Name == "Human Resources");

        }

        [Fact]
        public async Task Should_Create_A_Department()
        {
            //Act 
            var newDept = await _departmentAppService.CreateAsync(
                new CreateDepartmentDto { Name = "Test Departmani"}

                );
            //Assert
            newDept.Id.ShouldNotBe(System.Guid.Empty);
            newDept.Name.ShouldBe("Test Departmani");
        }
    }
}
