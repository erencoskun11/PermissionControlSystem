using Microsoft.AspNetCore.Authorization;
using PermissionControlSystem.Events;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2;
using System;
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
            LeaveRequest,           // Entity
            LeaveRequestDto,        // Get Output
            LeaveRequestListDto,    // Get List Output
            Guid,                   // Primary Key
            PagedAndSortedResultRequestDto, // Filter
            CreateLeaveRequestDto,  // Create Input
            UpdateLeaveRequestDto>, // Update Input
        ILeaveRequestAppService
    {
        private readonly IDistributedEventBus _distributedEventBus;

        public LeaveRequestAppService(
            IRepository<LeaveRequest, Guid> repository,
            IDistributedEventBus distributedEventBus)
            : base(repository)
        {
            _distributedEventBus = distributedEventBus;
        }

        // --- LİSTELEME (Hata Yakalama Bloğu İle) ---
        public override async Task<PagedResultDto<LeaveRequestListDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            try
            {
                return await base.GetListAsync(input);
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

        // 👇👇👇 DEĞİŞTİRDİĞİM YER BURASI (CreateAsync) 👇👇👇
        public override async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto input)
        {
            try
            {
                // 1. Kontrol: ID Boş mu?
                if (input.EmployeeId == Guid.Empty)
                {
                    throw new UserFriendlyException("HATA: Personel seçilmedi (EmployeeId boş geldi)!");
                }

                // 2. DÜZELTME: ObjectMapper yerine MANUEL OLUŞTURMA
                // Bu sayede input.EmployeeId'yi kesin olarak Entity'nin StaffId'sine atıyoruz.
                var entity = new LeaveRequest(
                    GuidGenerator.Create(), // Yeni GUID
                    input.EmployeeId,       // <-- StaffId alanına EmployeeId'yi basıyoruz
                    input.StartDate,
                    input.EndDate,
                    input.Reason
                );

                // 3. Kayıt
                await Repository.InsertAsync(entity, autoSave: true);

                // 4. Dönüş
                return ObjectMapper.Map<LeaveRequest, LeaveRequestDto>(entity);
            }
            catch (Exception ex)
            {
                var mesaj = ex.Message;
                if (ex.InnerException != null) mesaj += " | DETAY: " + ex.InnerException.Message;
                throw new UserFriendlyException("KAYIT HATASI: " + mesaj);
            }
        }
        // 👆👆👆 DEĞİŞİKLİK BİTTİ 👆👆👆

        // --- SİLME ---
        public override async Task DeleteAsync(Guid id)
        {
            await Repository.DeleteAsync(id, autoSave: true);
        }

        // --- ONAYLAMA ---
        public async Task ApproveAsync(Guid id)
        {
            var leaveRequest = await Repository.GetAsync(id);
            leaveRequest.Status = LeaveRequestStatus.Approved;
            leaveRequest.ManagerResponse = "İstek onaylandı.";
            await Repository.UpdateAsync(leaveRequest);

            // Event Fırlat (Outbox pattern ile)
            await _distributedEventBus.PublishAsync(
                new LeaveApprovedEto
                {
                    LeaveRequestId = leaveRequest.Id,
                    ManagerResponse = leaveRequest.ManagerResponse,
                    ApproverId = CurrentUser.Id ?? Guid.Empty
                });
        }

        // --- REDDETME ---
        public async Task RejectAsync(Guid id, string reason)
        {
            var leaveRequest = await Repository.GetAsync(id);
            leaveRequest.Status = LeaveRequestStatus.Rejected;
            leaveRequest.ManagerResponse = reason;
            await Repository.UpdateAsync(leaveRequest, autoSave: true);
        }
    }
}