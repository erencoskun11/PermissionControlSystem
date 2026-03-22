using PermissionControlSystem.Entities;
using PermissionControlSystem.Managers;
using Shouldly;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace PermissionControlSystem.Departments
{
    [Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
    public class DepartmentManagerTests : PermissionControlSystemDomainTestBase<PermissionControlSystemDomainTestModule>
    {
        private readonly DepartmentManager _departmentManager;
        private readonly IRepository<Department, Guid> _departmentRepository;
        private readonly IRepository<Employee, Guid> _employeeRepository;

        public DepartmentManagerTests()
        {
            _departmentManager = GetRequiredService<DepartmentManager>();
            _departmentRepository = GetRequiredService<IRepository<Department, Guid>>();
            _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        }


        [Fact]
        public async Task CreateAsync_Should_Throw_Exception_If_Name_Already_Exists()
        {
            var uniqueName = "IT_Create_Test"; // 🔥 Her testin kendi IT'si var!

            // ARRANGE: Veritabanına kaydediyoruz
            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(new Department(Guid.NewGuid(), uniqueName, "Bilgi İşlem"), autoSave: true);
            });

            // ACT & ASSERT: Aynı ismi tekrar eklemeye çalışıyoruz
            var exception = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    await _departmentManager.CreateAsync(uniqueName, "Yeni Açıklama");
                });
            });

            exception.Code.ShouldBe("Dept:001");
            exception.Message.ShouldContain($"'{uniqueName}' adında");
        }

        [Fact]
        public async Task ChangeNameAsync_Should_Throw_Exception_If_New_Name_Belongs_To_Another_Department()
        {
            var targetDeptId = Guid.NewGuid();
            var existingDeptId = Guid.NewGuid();

            var targetName = "HR_Change_Test"; // 🔥 Benzersiz isimler
            var existingName = "IT_Change_Test"; // 🔥 Benzersiz isimler

            // ARRANGE: İki farklı departmanı veritabanına yazıyoruz
            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(new Department(targetDeptId, targetName, "IK"), autoSave: true);
                await _departmentRepository.InsertAsync(new Department(existingDeptId, existingName, "Bilgi İşlem"), autoSave: true);
            });

            // ACT & ASSERT: HR'ın adını IT yapmaya çalışıyoruz
            var exception = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    var targetDept = await _departmentRepository.GetAsync(targetDeptId);
                    await _departmentManager.ChangeNameAsync(targetDept, existingName);
                });
            });

            exception.Code.ShouldBe("Dept:001");
        }

        [Fact]
        public async Task ChangeNameAsync_Should_Not_Throw_Exception_If_Name_Is_Same()
        {
            var deptId = Guid.NewGuid();
            var uniqueName = "IT_Same_Test"; // 🔥 Benzersiz isim

            // ARRANGE
            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(new Department(deptId, uniqueName, "Bilgi İşlem"), autoSave: true);
            });

            // ACT & ASSERT: Zaten kendi ismi olan ismi tekrar veriyoruz, hata vermemeli
            await Should.NotThrowAsync(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    var dept = await _departmentRepository.GetAsync(deptId);
                    await _departmentManager.ChangeNameAsync(dept, uniqueName);
                });
            });
        }


        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_Should_Create_Department_If_Name_Is_Unique()
        {
            // ACT
            var result = await WithUnitOfWorkAsync(async () =>
            {
                return await _departmentManager.CreateAsync("Muhasebe", "Finans İşleri");
            });

            // ASSERT
            result.ShouldNotBeNull();
            result.Name.ShouldBe("Muhasebe");
            result.Description.ShouldBe("Finans İşleri");
        }

      
        #endregion

        #region ChangeNameAsync Tests

        [Fact]
        public async Task ChangeNameAsync_Should_Change_Name_If_Unique()
        {
            // ARRANGE
            var dept = new Department(Guid.NewGuid(), "Eski Ad", "Açıklama");
            await WithUnitOfWorkAsync(async () => await _departmentRepository.InsertAsync(dept));

            // ACT
            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentManager.ChangeNameAsync(dept, "Yeni Ad");
            });

            // ASSERT
            dept.Name.ShouldBe("Yeni Ad");
        }

       
        #endregion

        #region CheckCanDeleteAsync Tests

        [Fact]
        public async Task CheckCanDeleteAsync_Should_Throw_Exception_If_Department_Has_Employees()
        {
            // ARRANGE: İçi dolu bir departman oluşturuyoruz
            var dept = new Department(Guid.NewGuid(), "Satış", "Satış Dept");
            var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), dept.Id, "Eren", "Coşkun", "eren@test.com", "123", "Uzman");

            await WithUnitOfWorkAsync(async () =>
            {
                await _departmentRepository.InsertAsync(dept);
                await _employeeRepository.InsertAsync(emp);
            });

            // ACT & ASSERT: İçi dolu departmanı silme kontrolünden geçiriyoruz
            var exception = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    await _departmentManager.CheckCanDeleteAsync(dept.Id);
                });
            });

            exception.Code.ShouldBe("Dept:002");
            exception.Message.ShouldContain("personel bulunduğu için");
        }

        [Fact]
        public async Task CheckCanDeleteAsync_Should_Not_Throw_Exception_If_Department_Is_Empty()
        {
            // ARRANGE: İçi boş, personelsiz bir departman kimliği
            var emptyDeptId = Guid.NewGuid();

            // ACT & ASSERT: Personel olmadığı için hata fırlatmamalı
            await Should.NotThrowAsync(async () =>
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    await _departmentManager.CheckCanDeleteAsync(emptyDeptId);
                });
            });
        }

        #endregion
    


        [Fact]
        public async Task Should_Create_Department_If_Name_Is_Unique()
        {
            Department result = null;

            await WithUnitOfWorkAsync(async () =>
            {
                result = await _departmentManager.CreateAsync("Muhasebe", "Finans İşleri");
            });

            result.ShouldNotBeNull();
            result.Name.ShouldBe("Muhasebe");
        }
        

      

    }
}