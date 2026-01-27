using PermissionControlSystem.AppUsers.Dtos;
using PermissionControlSystem.Users;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using PermissionControlSystem.AppUsers;

namespace PermissionControlSystem.AppUsers
{
    public class AppUserAppService : CrudAppService<AppUser, AppUserDto, Guid, PagedAndSortedResultRequestDto, CreateAppUserDto, UpdateAppUserDto>,
    IAppUserAppService
    {
        public AppUserAppService(IRepository<AppUser ,Guid> repo) : base(repo)
        {
            
        }
    }
}
