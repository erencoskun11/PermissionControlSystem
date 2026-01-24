using PermissionControlSystem.Departments.Dtos;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;
using Xunit;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppServiceTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IDepartmentAppService _departmentAppService;

        public DepartmentAppServiceTests()
        {
            _departmentAppService = GetRequiredService<IDepartmentAppService>();
        }

        [Fact]
        public async Task Should_Get_List_Of_Departments()
        {
            //Act
            var result = await _departmentAppService.GetListAsync(
            new PagedAndSortedResultRequestDto());

            //Assert
            result.TotalCount.ShouldBeGreaterThan(0);
            result.Items.ShouldContain(d => d.Name == "Human Resources");

        }

        [Fact]
        public async Task Should_Create_A_Department()
        {
            //Act 
            var newDept = await _departmentAppService.CreateAsync(
                new CreateDepartmentDto { Name = "Test Departmani" }

                );
            //Assert
            newDept.Id.ShouldNotBe(System.Guid.Empty);
            newDept.Name.ShouldBe("Test Departmani");
        }

        // 🟢 1. TEST: Yeni Kayıt Ekleme (Create)
        [Fact]
        public async Task Should_Create_A_Valid_Department()
        {
            // Arrange (Hazırlık)
            var input = new CreateDepartmentDto
            {
                Name = "AR-GE Test Ekibi",
                Description = "Yazılım testleri için oluşturuldu"
            };

            // Act (Eylem)
            var result = await _departmentAppService.CreateAsync(input);

            // Assert (Doğrulama)
            result.ShouldNotBeNull();
            result.Id.ShouldNotBe(Guid.Empty); // ID oluşmuş mu?
            result.Name.ShouldBe("AR-GE Test Ekibi");
        }

        // 🔴 2. TEST: Hatalı Kayıt Kontrolü (Validation)
        [Fact]
        public async Task Should_Fail_If_Name_Is_Empty()
        {
            // Arrange
            var input = new CreateDepartmentDto
            {
                Name = "", // Hatalı veri (Boş isim)
                Description = "İsimsiz departman olmaz"
            };

            // Act & Assert (Hata fırlatmasını bekliyoruz)
            // ABP'de validasyon hatası 'AbpValidationException' fırlatır.
            await Assert.ThrowsAsync<AbpValidationException>(async () =>
            {
                await _departmentAppService.CreateAsync(input);
            });
        }

        // 🟢 3. TEST: Tüm Listeyi Çekme (GetList)
        [Fact]
        public async Task Should_Get_List_Of_Departments2()
        {
            // Önce bir veri ekleyelim ki liste boş gelmesin
            await _departmentAppService.CreateAsync(new CreateDepartmentDto { Name = "Test Dept 1" });

            // Act
            var result = await _departmentAppService.GetListAsync(new PagedAndSortedResultRequestDto
            {
                MaxResultCount = 100
            });

            // Assert
            result.TotalCount.ShouldBeGreaterThan(0);
            result.Items.ShouldContain(d => d.Name == "Test Dept 1");
        }

        // 🟢 4. TEST: Güncelleme (Update)
        [Fact]
        public async Task Should_Update_Department()
        {
            // Arrange: Önce veri oluştur
            var createdDto = await _departmentAppService.CreateAsync(new CreateDepartmentDto { Name = "Eski İsim" });

            // Act: Güncelle
            var updateInput = new UpdateDepartmentDto
            {
                Name = "AR-GE (GÜNCELLENDİ)",
                Description = "İsim değişti"
            };
            var updatedDto = await _departmentAppService.UpdateAsync(createdDto.Id, updateInput);

            // Assert
            updatedDto.Name.ShouldBe("AR-GE (GÜNCELLENDİ)");
        }

        // 🟢 5. ve 6. TEST: Silme ve Hayalet Kayıt (Delete & Not Found)
        [Fact]
        public async Task Should_Delete_Department_And_Return_NotFound_Later()
        {
            // Arrange: Kayıt oluştur
            var createdDto = await _departmentAppService.CreateAsync(new CreateDepartmentDto { Name = "Silinecek Dept" });

            // Act: Sil
            await _departmentAppService.DeleteAsync(createdDto.Id);

            // Assert (Test 6): Silinen kaydı çağırmaya çalışınca hata almalıyız
            await Assert.ThrowsAnyAsync<EntityNotFoundException>(async () =>
            {
                await _departmentAppService.GetAsync(createdDto.Id);
            });
        }
    }
}
