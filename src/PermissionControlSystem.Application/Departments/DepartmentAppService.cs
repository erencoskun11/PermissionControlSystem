using PermissionControlSystem.Departments.Dtos; // DTO'ların olduğu namespace
using PermissionControlSystem.Departments2;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem.Departments
{
    // CrudAppService Sıralaması:
    // 1. Entity (Department)
    // 2. Get/List DTO (DepartmentDto)
    // 3. Key Tipi (Guid)
    // 4. Paged Result (Sayfalama isteği)
    // 5. Create DTO (CreateDepartmentDto) -> 🔥 Ekleme için
    // 6. Update DTO (UpdateDepartmentDto) -> 🔥 Güncelleme için

    public class DepartmentAppService :
        CrudAppService<
            Department,
            DepartmentDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateDepartmentDto,
            UpdateDepartmentDto>,
        IDepartmentAppService
    {
        public DepartmentAppService(IRepository<Department, Guid> repository)
            : base(repository)
        {
        }

        // 🔥 SİLME SORUNUNU ÇÖZEN KOD
        // Cache kullanmadığımız için GetListAsync'i ezmeye (override) gerek yok.
        // Ama silme işleminin veritabanına anında yazılması için bunu ezmek şart.
        public override async Task DeleteAsync(Guid id)
        {
            // autoSave: true diyerek "Transaction" beklemeden veritabanına işle diyoruz.
            await Repository.DeleteAsync(id, autoSave: true);
        }
    }
}