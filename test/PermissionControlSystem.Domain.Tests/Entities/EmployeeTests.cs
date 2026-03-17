using System;
using PermissionControlSystem.Entities;
using Shouldly;
using Xunit;

namespace PermissionControlSystem.Entities
{
    public class EmployeeTests
    {
        [Fact]
        public void Should_Update_Employee_Profile()
        {
            // Arrange
            var employee = new Employee(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EskiAd", "EskiSoyad", "eski@test.com", "111", "Çırak");

            // Act
            employee.UpdateProfile("YeniAd", "YeniSoyad", "Usta");

            // Assert
            employee.FirstName.ShouldBe("YeniAd");
            employee.LastName.ShouldBe("YeniSoyad");
            employee.Position.ShouldBe("Usta");
        }

        [Fact]
        public void Should_Update_Employee_Contact()
        {
            var employee = new Employee(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "B", "eski@test.com", "111", "C");

            employee.UpdateContact("yeni@test.com", "5554443322");

            employee.Email.ShouldBe("yeni@test.com");
            employee.PhoneNumber.ShouldBe("5554443322");
        }
    }
}