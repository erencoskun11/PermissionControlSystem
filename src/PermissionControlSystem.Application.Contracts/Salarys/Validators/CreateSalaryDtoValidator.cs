using FluentValidation;
using PermissionControlSystem.Salarys.Dtos;

namespace PermissionControlSystem.Salarys.Validators
{
    public class CreateSalaryDtoValidator : AbstractValidator<CreateSalaryDto>
    {
        public CreateSalaryDtoValidator()
        {
        }
    }
}