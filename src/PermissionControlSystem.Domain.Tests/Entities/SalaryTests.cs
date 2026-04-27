using System;
using PermissionControlSystem.Entities;
using Shouldly;
using Xunit;

namespace PermissionControlSystem.Entities
{
    public class SalaryTests
    {
        [Fact]
        public void Should_Create_Salary_With_Valid_Properties()
        {
            var id = Guid.NewGuid();
            var entity = new Salary(id);
            entity.Id.ShouldBe(id);
        }
    }
}