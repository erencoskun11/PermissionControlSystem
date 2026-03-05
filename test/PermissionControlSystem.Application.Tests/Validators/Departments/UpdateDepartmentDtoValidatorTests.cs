using FluentValidation.TestHelper;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Departments.Validators;
using Xunit;

namespace PermissionControlSystem.Validators.Departments
{
    public class UpdateDepartmentDtoValidatorTests
    {
        private readonly UpdateDepartmentDtoValidator _validator;

        public UpdateDepartmentDtoValidatorTests()
        {
            _validator = new UpdateDepartmentDtoValidator();
        }

        [Fact]
        public void Should_Not_Have_Error_When_Valid_Data_Is_Provided()
        {
            // ARRANGE
            var model = new UpdateDepartmentDto
            {
                Name = "IT Department", // 🔥 Fix: Provide a name to satisfy the validator
                Description = "Description"
            };

            // ACT
            var result = _validator.TestValidate(model);

            // ASSERT
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}