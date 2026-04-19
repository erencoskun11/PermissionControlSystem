using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;

namespace PermissionControlSystem.EventHandlers.LocalEvents.Leaves
{
    public class LeaveRequestUpdatedLocalEventHandler :
        ILocalEventHandler<EntityUpdatedEventData<LeaveRequest>>,
        ITransientDependency
    {
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
        private readonly IDistributedCache<LeaveBalanceCacheItem, string> _leaveBalanceCache;
        private readonly IDistributedCache<LeaveRequestCacheItem, string> _singleLeaveCache;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCache;
        private readonly IGuidGenerator _guidGenerator;
        private readonly INotificationService _notificationService;
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly LeaveRequestManager _leaveRequestManager;

        public LeaveRequestUpdatedLocalEventHandler(
            IRepository<OutboxMessage, Guid> outboxRepository,
            IDistributedCache<LeaveBalanceCacheItem, string> leaveBalanceCache,
            IDistributedCache<LeaveRequestCacheItem, string> singleLeaveCache,
            IDistributedCache<List<LeaveRequestCacheItem>, string> employeeLeavesCache,
            IGuidGenerator guidGenerator,
            INotificationService notificationService,
            IRepository<Employee, Guid> employeeRepository,
            LeaveRequestManager leaveRequestManager)
        {
            _outboxRepository = outboxRepository;
            _leaveBalanceCache = leaveBalanceCache;
            _singleLeaveCache = singleLeaveCache;
            _employeeLeavesCache = employeeLeavesCache;
            _guidGenerator = guidGenerator;
            _notificationService = notificationService;
            _employeeRepository = employeeRepository;
            _leaveRequestManager = leaveRequestManager;
        }

        public async Task HandleEventAsync(EntityUpdatedEventData<LeaveRequest> eventData)
        {
            var entity = eventData.Entity;

            // 🔥 SENIOR FIX: GetAsync (çökerten) yerine FindAsync (güvenli) kullanıyoruz!
            // Personel ve departman adını en başta çekiyoruz.
            var employee = await _employeeRepository.FindAsync(entity.EmployeeId, includeDetails: true);

            // 🔥 Eğer personel veritabanından silinmişse (Hayalet İzin), işlemi burada sessizce kes! 
            // Hangfire job'ı çökmekten kurtarıyoruz.
            if (employee == null)
            {
                return;
            }

            // 1. BİLDİRİM MANTIĞI
            string notificationText = "Bir izin talebi güncellendi.";
            string notificationLevel = "INFO";
            string eventType = "LEAVE_UPDATED";

            if (entity.Status == LeaveRequestStatus.Approved)
            {
                notificationText = "Bir izin talebi yönetici tarafından onaylandı.";
                notificationLevel = "SUCCESS";
                eventType = "LEAVE_APPROVED";
            }
            else if (entity.Status == LeaveRequestStatus.Rejected)
            {
                notificationText = $"İzin talebi reddedildi. Sebep: {entity.ManagerResponse ?? "Belirtilmedi"}";
                notificationLevel = "ERROR";
                eventType = "LEAVE_REJECTED";
            }

            await _notificationService.AddNotificationAsync(notificationText, notificationLevel, eventType);

            // 2. VERİ TAMAMLAMA (İstatistikler için şart)
            var deptName = employee.Department?.Name ?? "Belirtilmemiş";

            // İş günü (Hafta sonu hariç) hesabı
            int workingDays = _leaveRequestManager.CalculateWorkingDays(entity.StartDate, entity.EndDate);

            // 3. OUTBOX KAYDI (Gelişmiş Payload)
            var outboxMessage = new OutboxMessage(
                _guidGenerator.Create(),
                "LeaveRequest",
                JsonSerializer.Serialize(new
                {
                    Action = "Updated",
                    Id = entity.Id,
                    EmployeeId = entity.EmployeeId,
                    EmployeeName = employee.FullName,
                    DepartmentId = employee.DepartmentId,
                    DepartmentName = deptName,
                    LeaveType = (int)entity.LeaveType,
                    Status = (int)entity.Status,
                    StartDate = entity.StartDate,
                    EndDate = entity.EndDate,
                    CreationTime = entity.CreationTime,
                    DurationDays = workingDays,
                    Reason = entity.Reason ?? string.Empty,
                })
            );

            await _outboxRepository.InsertAsync(outboxMessage);

            // 4. CACHE TEMİZLİĞİ
            await _singleLeaveCache.RemoveAsync($"LeaveRequest_{entity.Id}");
            await _employeeLeavesCache.RemoveAsync($"EmployeeLeaves_{entity.EmployeeId}");
            await _leaveBalanceCache.RemoveAsync($"leave_balance_{entity.EmployeeId}");
        }
    }
}