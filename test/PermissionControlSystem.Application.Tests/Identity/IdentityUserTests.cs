using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace PermissionControlSystem.Identity
{
    public class IdentityUserTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IIdentityUserAppService _identityUserAppService;

        public IdentityUserTests()
        {
            _identityUserAppService = GetRequiredService<IIdentityUserAppService>();
        }

        [Fact]
        public async Task Should_Create_A_Valid_IdentityUser()
        {
            var input = new IdentityUserCreateDto
            {
                UserName = "testpersonel",
                Email = "test@sirket.com",
                Name = "Test",
                Surname = "Personel",
                Password = "REDACTED_SECRET",
                IsActive = true,
                LockoutEnabled = true
            };

            var result = await _identityUserAppService.CreateAsync(input);

            result.ShouldNotBeNull();
            result.UserName.ShouldBe("testpersonel");
            result.Email.ShouldBe("test@sirket.com");
        }

        [Fact]
        public async Task Should_Filter_Identity_Users()
        {
            await _identityUserAppService.CreateAsync(new IdentityUserCreateDto
            {
                UserName = "aranacak_kisi",
                Email = "aranan@sirket.com",
                Password = "REDACTED_SECRET"
            });

            var result = await _identityUserAppService.GetListAsync(new GetIdentityUsersInput
            {
                Filter = "aranacak_kisi"
            });

            result.Items.ShouldContain(u => u.UserName == "aranacak_kisi");
            result.Items.Any(u => u.UserName == "alakasiz_biri").ShouldBeFalse();
        }

        [Fact]
        public async Task Should_Update_IdentityUser_PhoneNumber()
        {
            var user = await _identityUserAppService.CreateAsync(new IdentityUserCreateDto
            {
                UserName = "guncellenecek",
                Email = "update@sirket.com",
                Password = "REDACTED_SECRET",
                Name = "EskiAd",
                Surname = "EskiSoyad"
            });

            var updateInput = new IdentityUserUpdateDto
            {
                UserName = user.UserName,
                Email = user.Email,
                Name = "EskiAd",
                Surname = "EskiSoyad",
                PhoneNumber = "05559998877",
                IsActive = true,
                LockoutEnabled = true
            };

            var updatedUser = await _identityUserAppService.UpdateAsync(user.Id, updateInput);

            updatedUser.PhoneNumber.ShouldBe("05559998877");
        }

        [Fact]
        public async Task Should_Delete_IdentityUser()
        {
            var user = await _identityUserAppService.CreateAsync(new IdentityUserCreateDto
            {
                UserName = "silinecek",
                Email = "sil@sirket.com",
                Password = "REDACTED_SECRET"
            });

            await _identityUserAppService.DeleteAsync(user.Id);

            var result = await _identityUserAppService.GetListAsync(new GetIdentityUsersInput
            {
                Filter = "silinecek"
            });

            result.Items.ShouldBeEmpty();
        }
    }
}