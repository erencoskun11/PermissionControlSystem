using System;
using FluentValidation.TestHelper;
using PermissionControlSystem.Leave.Dtos;
using Xunit;

namespace PermissionControlSystem.Validators.LeaveRequests
{
    public class CreateLeaveRequestDtoValidatorTests
    {
        private readonly CreateLeaveRequestDtoValidator _validator;

        public CreateLeaveRequestDtoValidatorTests()
        {
            _validator = new CreateLeaveRequestDtoValidator();
        }

        [Fact]
        public void Should_Not_Have_Error_When_LeaveRequest_Is_Valid()
        {
            var dto = new CreateLeaveRequestDto
            {
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(5),
                Reason = "Yıllık İznimi kullanmak istiyorum." // 10 karakterden uzun
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Should_Have_Error_When_StartDate_Is_In_The_Past()
        {
            var dto = new CreateLeaveRequestDto { StartDate = DateTime.Today.AddDays(-1) };
            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.StartDate)
                  .WithErrorMessage("Geçmiş tarihli izin alamazsınız.");
        }

        [Fact]
        public void Should_Have_Error_When_EndDate_Is_Before_StartDate()
        {
            var dto = new CreateLeaveRequestDto
            {
                StartDate = DateTime.Today.AddDays(5),
                EndDate = DateTime.Today.AddDays(2) // Bitiş başlangıçtan önce!
            };
            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.EndDate)
                  .WithErrorMessage("Bitiş tarihi, başlangıç tarihinden sonra olmalıdır.");
        }

        [Fact]
        public void Should_Have_Error_When_Reason_Is_Too_Short()
        {
            var dto = new CreateLeaveRequestDto { Reason = "Kısa" }; // 10 karakterden kısa
            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorMessage("Lütfen en az 10 karakterlik bir açıklama giriniz.");
        }
    }
}