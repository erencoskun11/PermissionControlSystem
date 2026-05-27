using PermissionControlSystem.Caching;
using PermissionControlSystem.Employees.Dtos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem.Employees
{
    public interface IEmployeeAppService : ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateEmployeeDto, UpdateEmployeeDto>
    {
        Task<List<EmployeeCacheItem>> GetCachedEmployeeListAsync(CancellationToken cancellationToken);
        Task<List<EmployeeDto>> SearchAsync(string keyword,CancellationToken cancellationToken);
        Task BulkCreateAsync(List<CreateEmployeeDto> input,CancellationToken cancellationToken);
    }
}