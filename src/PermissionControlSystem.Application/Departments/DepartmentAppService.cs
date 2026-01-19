
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Departments2;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppService :
        CrudAppService<
            Department,                 // 1. Hangi Entity?
            DepartmentDto,              // 2. Detayda ne göstereyim?
            Guid,                       // 3. ID tipi ne?
            PagedAndSortedResultRequestDto, // 4. Listeleme ayarları
            CreateDepartmentDto,        // 5. Oluştururken ne isteyeyim?
            UpdateDepartmentDto>,       // 6. Güncellerken ne isteyeyim?
        IDepartmentAppService           // Hangi arayüzü uyguluyorum?
    {
        public DepartmentAppService(IRepository<Department, Guid> repository)
            : base(repository)
        {
            // Burası boş kalabilir, CrudAppService her şeyi bizim için yapıyor!
        }
    }
}