using FluentValidation;
using PermissionControlSystem.Departments.Dtos;

namespace PermissionControlSystem.Departments.Validators
{
    public class CreateDepartmentDtoValidator : AbstractValidator<CreateDepartmentDto>
    {
        public CreateDepartmentDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Departman adı zorunludur.")
                .MaximumLength(100).WithMessage("Departman adı 100 karakteri geçemez.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Açıklama çok uzun.");
        }
    }
}