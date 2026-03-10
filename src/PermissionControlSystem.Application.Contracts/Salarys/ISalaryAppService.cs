using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Salarys.Dtos;
using PermissionControlSystem.Models;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem.Salarys
{
    public interface ISalaryAppService :
        ICrudAppService<
            SalaryDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateSalaryDto,
            UpdateSalaryDto>
    {
        Task<List<SalaryCacheItem>> GetSalarysAsync();
        Task<List<SalaryDto>> SearchFromElasticAsync(string keyword);
    }
}