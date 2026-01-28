using Microsoft.AspNetCore.Authorization;
using PermissionControlSystem.Events;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2;
using PermissionControlSystem.Employees; 
using System;
using System.Collections.Generic; 
using System.Threading.Tasks;
using System.Linq; // OrderBy için
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

                // B. DTO'ya çevir
                var dtos = ObjectMapper.Map<List<LeaveRequest>, List<LeaveRequestListDto>>(items);

                // C. 3. DEĞİŞİKLİK: Her bir izin için Personel Adını bul ve doldur
                foreach (var dto in dtos)
                {
                    if (dto.EmployeeId != Guid.Empty)
                    {
                        // Bu ID'ye sahip personel var mı?
                        var emp = await _employeeRepository.FindAsync(dto.EmployeeId);
                        if (emp != null)
                        {
                            dto.EmployeeName = emp.FullName; // İsim bulundu!
                        }
                        else
                        {
                            dto.EmployeeName = "Silinmiş Personel";
                        }
                    }
                }

                return new PagedResultDto<LeaveRequestListDto>(totalCount, dtos);
            }
            catch (Exception ex)
            {
                var hataMesaji = ex.Message;
                if (ex.InnerException != null)
                {
                    hataMesaji += " || DETAY: " + ex.InnerException.Message;
                }
                throw new UserFriendlyException("LİSTELEME HATASI: " + hataMesaji);
            }
        }

        // --- EKLEME (Create) - Zaten düzeltmiştik, aynen koruyoruz ---
        public override async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto input)
        {
            try
            {
                if (input.EmployeeId == Guid.Empty)
                {
                    throw new UserFriendlyException("HATA: Personel seçilmedi (EmployeeId boş geldi)!");
                }

                var entity = new LeaveRequest(
                    GuidGenerator.Create(),
                    input.EmployeeId,
                    input.StartDate,
                    input.EndDate,
                    input.Reason
                );

                await Repository.InsertAsync(entity, autoSave: true);

                return ObjectMapper.Map<LeaveRequest, LeaveRequestDto>(entity);
            }
            catch (Exception ex)
            {
                var mesaj = ex.Message;
                if (ex.InnerException != null) mesaj += " | DETAY: " + ex.InnerException.Message;
                throw new UserFriendlyException("KAYIT HATASI: " + mesaj);
            }
        }

        public override async Task DeleteAsync(Guid id)
        {
            await Repository.DeleteAsync(id, autoSave: true);
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