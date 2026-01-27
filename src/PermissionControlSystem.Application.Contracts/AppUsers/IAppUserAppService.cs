using PermissionControlSystem.AppUsers.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem.AppUsers
{
    public interface IAppUserAppService : IApplicationService
    {
        Task<PagedResultDto<AppUserDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task<AppUserDto> GetAsync(Guid id);
        Task<AppUserDto> CreateAsync(CreateAppUserDto input);
        Task<AppUserDto> UpdateAsync(Guid id, UpdateAppUserDto input);
        Task DeleteAsync(Guid id);
    }
}
