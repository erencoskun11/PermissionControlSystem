using Microsoft.AspNetCore.Authorization;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem.Leaves
{
    [Authorize]
    public class LeaveRequestAppService : 
        CrudAppService<
            LeaveRequest,
            LeaveRequestDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateLeaveRequestDto,
            UpdateLeaveRequestDto>,
        ILeaveRequestAppService
    {
        public LeaveRequestAppService(IRepository<LeaveRequest,Guid> repository)
            : base(repository)
        {
        }

        public async Task ApproveAsync(Guid id)
        {
            var leaveRequest = await Repository.GetAsync(id);

            leaveRequest.Status = LeaveRequestStatus.Approved;
            leaveRequest.ManagerResponse = "İstek onaylandı.";

            await Repository.UpdateAsync(leaveRequest);
        }

        public async Task RejectAsync(Guid id, string reason)
        {
            var leaveRequest = await Repository.GetAsync(id);

            leaveRequest.Status = LeaveRequestStatus.Rejected;
            leaveRequest.ManagerResponse = reason;
        
            await Repository.UpdateAsync(leaveRequest);

        }

    }
}
