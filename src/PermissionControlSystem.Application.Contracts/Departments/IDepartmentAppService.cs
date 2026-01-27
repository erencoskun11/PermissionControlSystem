using System;
using PermissionControlSystem.Departments.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem.Departments
{
    public interface IDepartmentAppService :
        ICrudAppService<
            DepartmentDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateDepartmentDto,
            UpdateDepartmentDto>
    {
        // Not: Listeleme için otomatik DepartmentDto kullanılır, 
        // özelleştirmek isterseniz GetListAsync'i override edeceğiz.
    }
}