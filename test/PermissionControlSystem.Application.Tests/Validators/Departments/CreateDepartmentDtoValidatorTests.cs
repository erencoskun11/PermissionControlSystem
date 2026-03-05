using FluentValidation.TestHelper;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Departments.Validators;
using Xunit;

namespace PermissionControlSystem.Validators.Departments
{
    public class CreateDepartmentDtoValidatorTests
    {
        private readonly CreateDepartmentDtoValidator _validator;

        public CreateDepartmentDtoValidatorTests()
        {
            _validator = new CreateDepartmentDtoValidator();
        }

        [Fact]
        public void Should_Not_Have_Error_When_Dto_Is_Valid()
        {
            var dto = new CreateDepartmentDto { Name = "IT", Description = "Bilgi İşlem" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Should_Have_Error_When_Name_Is_Empty()
        {
            var dto = new CreateDepartmentDto { Name = "", Description = "Açıklama" };
            var result = _validator.TestValidate(dto);

            // Name alanı için hata fırlatmasını bekliyoruz
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorMessage("Departman adı zorunludur.");
        }

        [Fact]
        public void Should_Have_Error_When_Name_Exceeds_100_Characters()
        {
            var dto = new CreateDepartmentDto { Name = new string('A', 101) };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }
    }
}