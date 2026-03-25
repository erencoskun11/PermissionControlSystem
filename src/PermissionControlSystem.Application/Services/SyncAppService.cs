using Microsoft.AspNetCore.Mvc;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Models;
using System;
using System.Linq;
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
        public async Task<string> SyncAllDataToElasticAsync()
        {
            try
            {
                // 1. DEPARTMANLAR
                var departments = await _departmentRepository.GetListAsync();
                var deptModels = departments.Select(d => new DepartmentIndexModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description ?? "",
                    LastModificationTime = DateTime.UtcNow
                }).ToList();
                await _elasticSearchService.BulkIndexDepartmentsAsync(deptModels);

                // 2. ÇALIŞANLAR
                // 🔥 SENIOR FIX: Önce ToList yapıyoruz ki ?. operatörü hata vermesin
                var employeeEntities = await _employeeRepository.GetListAsync(includeDetails: true);

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
                await _elasticSearchService.BulkIndexEmployeesAsync(empModels);

                // 3. İZİN TALEPLERİ
                // 🔥 SENIOR FIX: Veriyi önce veritabanından çekiyoruz (CalculateWorkingDays SQL'de çalışmaz!)
                var leaveEntities = await _leaveRequestRepository.GetListAsync(includeDetails: true);

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

                await _elasticSearchService.BulkIndexLeaveRequestsAsync(leaveModels);

                return $"Başarılı! {deptModels.Count} Departman, {empModels.Count} Çalışan, {leaveModels.Count} İzin Talebi eşitlendi. Dashboard artık %100 doğru!";
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Senkronizasyon hatası: {ex.Message}");
            }
        }

       
    }
}