using System;
using FluentValidation.TestHelper;
using PermissionControlSystem.Leave.Dtos;
using PermissionControlSystem.Leave.Validators;
using Xunit;

namespace PermissionControlSystem.Validators.LeaveRequests
{
    public class UpdateLeaveRequestDtoValidatorTests
    {
        private readonly UpdateLeaveRequestDtoValidator _validator;

        public UpdateLeaveRequestDtoValidatorTests()
        {
            _validator = new UpdateLeaveRequestDtoValidator();
        }

        [Fact]
        public void Should_Have_Error_When_Updating_With_Past_Date()
        {
            var dto = new UpdateLeaveRequestDto
            {
                StartDate = DateTime.Today.AddDays(-5), // Geçmiş tarih
                EndDate = DateTime.Today.AddDays(5),
                Reason = "Geçerli bir sebep yazısı."
            };

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.StartDate)
                  .WithErrorMessage("İzin tarihini geçmişe alamazsınız.");
        }

        [Fact]
        public void Should_Not_Have_Error_When_Update_Is_Valid()
        {
            var dto = new UpdateLeaveRequestDto
            {
                StartDate = DateTime.Today.AddDays(2),
                EndDate = DateTime.Today.AddDays(10),
                Reason = "Tarihleri güncelliyorum."
            };

            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}