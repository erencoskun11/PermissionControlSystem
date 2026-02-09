using Microsoft.AspNetCore.Authorization;
using PermissionControlSystem.Employees;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Events;
using PermissionControlSystem.Leave;
using PermissionControlSystem.Leave.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.Leaves
{
    [Authorize]
    public class LeaveRequestAppService :
        CrudAppService<
            LeaveRequest,
            LeaveRequestDto,
            LeaveRequestListDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateLeaveRequestDto,
            UpdateLeaveRequestDto>,
        ILeaveRequestAppService
    {
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly IRepository<Employee, Guid> _employeeRepository;

        public LeaveRequestAppService(
            IRepository<LeaveRequest, Guid> repository,
            IRepository<Employee, Guid> employeeRepository,
            IDistributedEventBus distributedEventBus)
            : base(repository)
        {
            _distributedEventBus = distributedEventBus;
            _employeeRepository = employeeRepository;
        }

        public override async Task<PagedResultDto<LeaveRequestListDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            try
            {
                var query = await Repository.GetQueryableAsync();
                var totalCount = await AsyncExecuter.CountAsync(query);

                var items = await AsyncExecuter.ToListAsync(
                    query.OrderByDescending(x => x.CreationTime)
                         .Skip(input.SkipCount)
                         .Take(input.MaxResultCount)
                );

                var dtos = ObjectMapper.Map<List<LeaveRequest>, List<LeaveRequestListDto>>(items);

                foreach (var dto in dtos)
                {
                    if (dto.EmployeeId != Guid.Empty)
                    {
                        var emp = await _employeeRepository.FindAsync(dto.EmployeeId);
                        dto.EmployeeName = emp != null ? $"{emp.FirstName} {emp.LastName}" : "Silinmiş Personel";
                    }
                }

                return new PagedResultDto<LeaveRequestListDto>(totalCount, dtos);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ex.Message);
            }
        }

        public override async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto input)
        {
            try
            {
                if (input.EmployeeId == Guid.Empty)
                    throw new UserFriendlyException("Personel seçilmedi.");

                if (input.StartDate >= input.EndDate)
                    throw new UserFriendlyException("Başlangıç tarihi bitiş tarihinden önce olmalıdır.");

                var requestedDays = (input.EndDate - input.StartDate).TotalDays;

                var query = await Repository.GetQueryableAsync();

                var existingLeaves = query.Where(x =>
                    x.EmployeeId == input.EmployeeId &&
                    x.StartDate.Year == input.StartDate.Year &&
                    x.Status != LeaveRequestStatus.Rejected
                    ).ToList();

                double usedDays = existingLeaves.Sum(x => (x.EndDate - x.StartDate).TotalDays);

                if ((usedDays + requestedDays) > 20)
                {
                    throw new UserFriendlyException($"Yıllık izin kotası aşıldı. Kullanılan: {usedDays}, İstenen: {requestedDays}");
                }

                var entity = new LeaveRequest(
                    GuidGenerator.Create(),
                    input.EmployeeId,
                    input.LeaveTypeId,
                    input.StartDate,
                    input.EndDate,
                    input.Reason
                );

                await Repository.InsertAsync(entity, autoSave: true);

                return ObjectMapper.Map<LeaveRequest, LeaveRequestDto>(entity);
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException(ex.Message);
            }
        }

        public async Task ApproveAsync(Guid id)
        {
            var leaveRequest = await Repository.GetAsync(id);

            leaveRequest.Status = LeaveRequestStatus.Approved;
            leaveRequest.ManagerResponse = "İstek onaylandı.";

            await Repository.UpdateAsync(leaveRequest);

            await _distributedEventBus.PublishAsync(
                new LeaveApprovedEto
                {
                    EventId = Guid.NewGuid(),
                    LeaveRequestId = leaveRequest.Id,
                    ManagerResponse = leaveRequest.ManagerResponse,
                    ApproverId = CurrentUser.Id ?? Guid.Empty
                });
        }

        public async Task RejectAsync(Guid id, string reason)
        {
            var leaveRequest = await Repository.GetAsync(id);

            leaveRequest.Status = LeaveRequestStatus.Rejected;
            leaveRequest.ManagerResponse = reason;

            await Repository.UpdateAsync(leaveRequest, autoSave: true);
        }
    }
}