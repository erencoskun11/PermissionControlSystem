using FluentValidation.TestHelper;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Employees.Validators;
using Xunit;

namespace PermissionControlSystem.Validators.Employees
{
    public class UpdateEmployeeDtoValidatorTests
    {
        private readonly UpdateEmployeeDtoValidator _validator;

        public UpdateEmployeeDtoValidatorTests()
        {
            _validator = new UpdateEmployeeDtoValidator();
        }

        [Fact]
        public void Should_Have_Error_When_Updating_With_Empty_First_Name()
        {
            var dto = new UpdateEmployeeDto
            {
                FirstName = "", // Güncellerken ismini silmeye çalışıyor!
                LastName = "Yılmaz",
                Email = "test@sirket.com",
                PhoneNumber = "111",
                Position = "Uzman"
            };

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorMessage("Personel adı boş bırakılamaz.");
        }

        [Fact]
        public void Should_Not_Have_Error_When_Update_Is_Valid()
        {
            var dto = new UpdateEmployeeDto
            {
                FirstName = "Yeni İsim",
                LastName = "Yeni Soyisim",
                Email = "yeni@sirket.com",
                PhoneNumber = "5551112233",
                Position = "Kıdemli Uzman"
            };

            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}