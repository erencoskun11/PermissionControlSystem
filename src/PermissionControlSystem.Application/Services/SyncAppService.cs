using Microsoft.AspNetCore.Mvc;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem.Services
{
    [RemoteService(IsEnabled = true)]
    public class SyncAppService : ApplicationService, ISyncAppService
    {
        private readonly IRepository<Department, Guid> _departmentRepository;
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IRepository<LeaveRequest, Guid> _leaveRequestRepository;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly LeaveRequestManager _leaveRequestManager;

        // 🔥 SENIOR MİMARİ: Tüm uygulama genelinde aynı anda SADECE 1 kişinin geçmesine izin veren "Turnike"
        private static readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        public SyncAppService(
            IRepository<Department, Guid> departmentRepository,
            IRepository<Employee, Guid> employeeRepository,
            IRepository<LeaveRequest, Guid> leaveRequestRepository,
            IElasticSearchService elasticSearchService,
            LeaveRequestManager leaveRequestManager)
        {
            _departmentRepository = departmentRepository;
            _employeeRepository = employeeRepository;
            _leaveRequestRepository = leaveRequestRepository;
            _elasticSearchService = elasticSearchService;
            _leaveRequestManager = leaveRequestManager;
        }

        [HttpPost("sync-all-to-elastic")]
        public async Task<string> SyncAllDataToElasticAsync(CancellationToken cancellationToken = default)
        {
            // 🔥 TURNİKE KONTROLÜ: 
            // WaitAsync(0) diyoruz, yani kapıda bekleme! İçeride biri varsa direkt false dön.
            if(!await _syncLock.WaitAsync(0,cancellationToken))
            {
                // İçeride zaten bir senkronizasyon dönüyorsa, ikinci basana bu kibar hatayı fırlatıyoruz.
                throw new UserFriendlyException("Şu anda halihazırda bir senkronizasyon işlemi devam ediyor. Lütfen birkaç dakika bekleyip tekrar deneyin.");
            }

            try
            {
                // 1. DEPARTMANLAR
                var departments = await _departmentRepository.GetListAsync(cancellationToken: cancellationToken);
                var deptModels = departments.Select(d => new DepartmentIndexModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description ?? "",
                    LastModificationTime = DateTime.UtcNow
                }).ToList();
                await _elasticSearchService.BulkIndexDepartmentsAsync(deptModels,cancellationToken);

                // 2. ÇALIŞANLAR
                var employeeEntities = await _employeeRepository.GetListAsync(includeDetails: true,cancellationToken);

                var empModels = employeeEntities.Select(e => new EmployeeIndexModel
                {
                    Id = e.Id,
                    UserId = e.UserId,
                    DepartmentId = e.DepartmentId,
                    DepartmentName = e.Department?.Name ?? "Belirtilmemiş",
                    FullName = $"{e.FirstName} {e.LastName}",
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Position = e.Position,
                    Email = e.Email,
                    PhoneNumber = e.PhoneNumber
                }).ToList();
                await _elasticSearchService.BulkIndexEmployeesAsync(empModels, cancellationToken);

                // 3. İZİN TALEPLERİ
                // 🔥 SENIOR FIX: Veriyi önce veritabanından çekiyoruz (CalculateWorkingDays SQL'de çalışmaz!)
                var leaveEntities = await _leaveRequestRepository.GetListAsync(includeDetails: true, cancellationToken);

                var leaveModels = leaveEntities.Select(l => new LeaveIndexModel
                {
                    Id = l.Id,
                    EmployeeId = l.EmployeeId,
                    EmployeeName = l.Employee != null ? $"{l.Employee.FirstName} {l.Employee.LastName}" : "Bilinmiyor",
                    DepartmentName = l.Employee?.Department?.Name ?? "Belirtilmemiş",
                    Description = l.Reason ?? "",
                    CreationTime = l.CreationTime,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    Status = (int)l.Status,
                    LeaveType = (int)l.LeaveType,
                    // 🔥 Belleğe aldığımız için bu metod artık tıkır tıkır çalışacak!
                    DurationDays = _leaveRequestManager.CalculateWorkingDays(l.StartDate, l.EndDate)
                }).ToList();

                await _elasticSearchService.BulkIndexLeaveRequestsAsync(leaveModels, cancellationToken);

                return $"Başarılı! {deptModels.Count} Departman, {empModels.Count} Çalışan, {leaveModels.Count} İzin Talebi eşitlendi. Dashboard artık %100 doğru!";
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Senkronizasyon hatası: {ex.Message}");
            }
            finally
            {
                // İşlem tamamlandığında veya hata oluştuğunda, kapıyı açmayı unutmayalım!
                _syncLock.Release();
            }
        }

       
    }
}