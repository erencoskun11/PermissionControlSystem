using FluentValidation;
using PermissionControlSystem.Departments.Dtos;

namespace PermissionControlSystem.Departments.Validators
{
    public class UpdateDepartmentDtoValidator : AbstractValidator<UpdateDepartmentDto>
    {
        public UpdateDepartmentDtoValidator()
        {
            RuleFor(x => x.Name)
                 .NotEmpty().WithMessage("Departman adi zorunludur");
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Açıklama çok uzun.");
        }
    }
}