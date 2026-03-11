using System;
using FluentValidation.TestHelper;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Employees.Validators;
using Xunit;

namespace PermissionControlSystem.Validators.Employees
{
    public class CreateEmployeeDtoValidatorTests
    {
        private readonly CreateEmployeeDtoValidator _validator;

        public CreateEmployeeDtoValidatorTests()
        {
            _validator = new CreateEmployeeDtoValidator();
        }

        [Fact]
        public void Should_Not_Have_Error_When_Dto_Is_Valid()
        {
            // Arrange
            var dto = new CreateEmployeeDto
            {
                FirstName = "Ali",
                LastName = "Yılmaz",
                Email = "ali.yilmaz@sirket.com",
                PhoneNumber = "05554443322",
                Position = "Yazılım Uzmanı",
                DepartmentId = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Should_Have_Error_When_Email_Is_Invalid()
        {
            var dto = new CreateEmployeeDto { Email = "alidegilbu" }; // Format hatalı!
            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                  .WithErrorMessage("Lütfen geçerli bir e-posta adresi giriniz.");
        }

        [Fact]
        public void Should_Have_Error_When_Required_Fields_Are_Empty()
        {
            var dto = new CreateEmployeeDto(); // Her şey boş gönderiliyor

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.FirstName);
            result.ShouldHaveValidationErrorFor(x => x.LastName);
            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
            result.ShouldHaveValidationErrorFor(x => x.Position);
            result.ShouldHaveValidationErrorFor(x => x.DepartmentId);
        }
    }
}