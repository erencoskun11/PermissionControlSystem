
using FluentValidation;
using PermissionControlSystem.Employees.Dtos; // Dto'nun bulunduğu kendi namespace'ine göre düzeltirsin

namespace PermissionControlSystem.Employees.Validators
{
    public class CreateEmployeeDtoValidator : AbstractValidator<CreateEmployeeDto>
    {
        public CreateEmployeeDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Personel adı zorunludur.")
                .MaximumLength(50).WithMessage("Ad 50 karakterden uzun olamaz.");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Personel soyadı zorunludur.")
                .MaximumLength(50).WithMessage("Soyad 50 karakterden uzun olamaz.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-Posta adresi zorunludur.")
                .EmailAddress().WithMessage("Lütfen geçerli bir e-posta adresi giriniz.")
                .MaximumLength(100).WithMessage("E-Posta adresi çok uzun.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Telefon numarası zorunludur.")
                .MaximumLength(20).WithMessage("Telefon numarası 20 karakteri geçemez.");

            RuleFor(x => x.Position)
                .NotEmpty().WithMessage("Pozisyon / Unvan bilgisi zorunludur.")
                .MaximumLength(100).WithMessage("Pozisyon adı çok uzun.");

            RuleFor(x => x.DepartmentId)
                .NotEmpty().WithMessage("Personelin bağlı olduğu bir departman seçilmelidir.");
        }
    }
}