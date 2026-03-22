using System;
using PermissionControlSystem.Entities;
using Shouldly;
using Xunit;

namespace PermissionControlSystem.Entities
{
    public class DepartmentTests
    {
        [Fact]
        public void Should_Create_Department_With_Valid_Properties()
        {
            // Arrange
            var id = Guid.NewGuid();
            var name = "Bilgi İşlem";
            var description = "IT Departmanı";

            // Act
            var department = new Department(id, name, description);

            // Assert
            department.Id.ShouldBe(id);
            department.Name.ShouldBe(name);
            department.Description.ShouldBe(description);

            // En kritik kontrol: Liste null olmamalı, boş bir liste olarak initialize edilmeli!
            department.Employees.ShouldNotBeNull();
            department.Employees.ShouldBeEmpty();
        }
    }
}