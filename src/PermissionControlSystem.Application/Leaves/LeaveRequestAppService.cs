using Microsoft.AspNetCore.Authorization;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2; // Leaves2 (Veritabanı tablosu)
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem.Leaves
{
    [Authorize]
    public class LeaveRequestAppService :
        CrudAppService<
            LeaveRequest,           // 1. Entity
            LeaveRequestDto,        // 2. Tekli Getirirken (GET)
            LeaveRequestListDto,    // 3. Listeleme Yaparken (GET LIST)
            Guid,                   // 4. ID Tipi
            PagedAndSortedResultRequestDto, // 5. Filtreler
            CreateLeaveRequestDto,  // 6. Ekleme DTO
            UpdateLeaveRequestDto>, // 7. Güncelleme DTO
        ILeaveRequestAppService
    {
        public LeaveRequestAppService(IRepository<LeaveRequest, Guid> repository)
            : base(repository)
        {
        }

        // 👇👇👇 EKLEDİĞİMİZ HATA YAKALAYICI METOD BURADA 👇👇👇
        public override async Task<PagedResultDto<LeaveRequestListDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            try
            {
                // Standart listeleme işlemini yapmayı dene
                return await base.GetListAsync(input);
            }
            catch (Exception ex)
            {
                // Hata detayını yakala
                var hataMesaji = ex.Message;
                if (ex.InnerException != null)
                {
                    hataMesaji += " || DETAY: " + ex.InnerException.Message;
                }

                // Swagger'a fırlat
                throw new UserFriendlyException("LİSTELEME HATASI: " + hataMesaji);
            }
        }
        // 👆👆👆 EKLEME BİTTİ 👆👆👆


        // 1. EKLEME (Create)
        public override async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto input)
        {
            try
            {
                if (input.StaffId == Guid.Empty)
                {
                    throw new UserFriendlyException("HATA: Personel seçimi yapılmadı (StaffId boş)!");
                }

                // DTO -> Entity Çevirimi
                var entity = ObjectMapper.Map<CreateLeaveRequestDto, LeaveRequest>(input);

                // Veritabanına Kaydet
                await Repository.InsertAsync(entity, autoSave: true);

                // Geriye Dönüş
                return ObjectMapper.Map<LeaveRequest, LeaveRequestDto>(entity);
            }
            catch (Exception ex)
            {
                var mesaj = ex.Message;
                if (ex.InnerException != null) mesaj += " | DETAY: " + ex.InnerException.Message;
                throw new UserFriendlyException("KAYIT HATASI: " + mesaj);
            }
        }

        // 2. SİLME (Delete)
        public override async Task DeleteAsync(Guid id)
        {
            await Repository.DeleteAsync(id, autoSave: true);
        }

        // 3. ONAYLAMA (Approve)
        public async Task ApproveAsync(Guid id)
        {
            var leaveRequest = await Repository.GetAsync(id);
            leaveRequest.Status = LeaveRequestStatus.Approved;
            leaveRequest.ManagerResponse = "İstek onaylandı.";
            await Repository.UpdateAsync(leaveRequest, autoSave: true);
        }

        // 4. REDDETME (Reject)
        public async Task RejectAsync(Guid id, string reason)
        {
            var leaveRequest = await Repository.GetAsync(id);
            leaveRequest.Status = LeaveRequestStatus.Rejected;
            leaveRequest.ManagerResponse = reason;
            await Repository.UpdateAsync(leaveRequest, autoSave: true);
        }
    }
}