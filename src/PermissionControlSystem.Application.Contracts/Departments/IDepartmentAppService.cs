using Nest;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        Task BulkCreateAsync(List<CreateDepartmentDto> input,CancellationToken cancellationToken = default);

        // Not: Listeleme için otomatik DepartmentDto kullanılır, 
        // özelleştirmek isterseniz GetListAsync'i override edeceğiz.
        Task<List<DepartmentCacheItem>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
        Task<List<DepartmentDto>> SearchFromElasticAsync(string keyword, CancellationToken cancellationToken = default);
    }
}