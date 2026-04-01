using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Events;
using PermissionControlSystem.Events.LeaveRequest;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;

namespace PermissionControlSystem.EventHandlers.LocalEvents.Leaves
{
    public class LeaveRequestCreatedLocalEventHandler : ILocalEventHandler<LeaveRequestCreatedEvent>, ITransientDependency
    {
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly IDistributedCache<LeaveBalanceCacheItem, string> _leaveBalanceCache;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCache;
        private readonly IGuidGenerator _guidGenerator;
        private readonly INotificationService _notificationService;
        private readonly IRepository<Employee, Guid> _employeeRepository; // 🔥 Departman adını bulmak için ekledik
        private readonly LeaveRequestManager _leaveRequestManager;

        public LeaveRequestCreatedLocalEventHandler(
            IRepository<OutboxMessage, Guid> outboxRepository,
            IDistributedEventBus distributedEventBus,
            IDistributedCache<LeaveBalanceCacheItem, string> leaveBalanceCache,
            IDistributedCache<List<LeaveRequestCacheItem>, string> employeeLeavesCache,
            IGuidGenerator guidGenerator,
            INotificationService notificationService,
            IRepository<Employee, Guid> employeeRepository,
            LeaveRequestManager leaveRequestManager)
        {
            _outboxRepository = outboxRepository;
            _distributedEventBus = distributedEventBus;
            _leaveBalanceCache = leaveBalanceCache;
            _employeeLeavesCache = employeeLeavesCache;
            _guidGenerator = guidGenerator;
            _notificationService = notificationService;
            _employeeRepository = employeeRepository;
            _leaveRequestManager = leaveRequestManager;
        }

        public async Task HandleEventAsync(LeaveRequestCreatedEvent eventData)
        {
            await _notificationService.AddNotificationAsync(
                $"{eventData.EmployeeName} adlı personel için yeni izin talebi oluşturuldu.",
                "INFO",
                "LEAVE_CREATED"
            );

            // 🔥 SENIOR FIX 1: Departman adı ve net iş günü hesaplanıyor
            var employee = await _employeeRepository.GetAsync(eventData.EmployeeId, includeDetails: true);
            var deptName = employee.Department?.Name ?? "Belirtilmemiş";
            int durationDays = _leaveRequestManager.CalculateWorkingDays(eventData.StartDate, eventData.EndDate);

            // 🔥 SENIOR FIX 2: SÖZLEŞME (CONTRACT) TAMAMLANDI!
            var outboxMessage = new OutboxMessage(
                _guidGenerator.Create(),
                "LeaveRequest", // 🔥 TİP SABİTLENDİ
                JsonSerializer.Serialize(new
                {
                    Action = "Created",
                    Id = eventData.LeaveRequestId,
                    EmployeeId = eventData.EmployeeId,
                    EmployeeName = eventData.EmployeeName,
                    DepartmentId = employee.DepartmentId,
                    DepartmentName = deptName,
                    LeaveType = (int)eventData.LeaveType,
                    Status = (int)eventData.Status,
                    StartDate = eventData.StartDate,
                    EndDate = eventData.EndDate,
                    CreationTime = eventData.CreationTime,
                    DurationDays = durationDays,
                    Reason = eventData.Reason ?? string.Empty
                })
            );

            await _outboxRepository.InsertAsync(outboxMessage);

            // =========================================================================
            // 🚀 4. İŞLEM: DUAL-QUEUE (VIP ŞERİT - RABBITMQ YÖNLENDİRMESİ)
            // =========================================================================
            bool isUrgent = eventData.LeaveType == LeaveType.Health;

            if (isUrgent)
            {
                // 🏎️ VIP ŞERİT: Sağlık izni doğrudan UrgentLeaveRequestCreatedEto olarak RabbitMQ'ya gider!
                // VipLeaveRequestWorker.cs arka planda bunu anında yakalayıp müdüre acil mail atacak.
                await _distributedEventBus.PublishAsync(new UrgentLeaveRequestCreatedEto
                {
                    LeaveRequestId = eventData.LeaveRequestId,
                    StaffId = eventData.EmployeeId,
                    EmployeeName = eventData.EmployeeName,
                    Message = $"🚨 ACİL SAĞLIK DURUMU: {eventData.EmployeeName} adlı personel Hastalık İzni talep etti!",
                    StartDate = eventData.StartDate,
                    EndDate = eventData.EndDate,
                    Reason = eventData.Reason ?? "Belirtilmemiş",
                    CriticalLevel = "MedicalEmergency"
                });
            }
            else
            {
                // 🚚 STANDART ŞERİT: Diğer tüm izinler normal kuyruğa gider.
                await _distributedEventBus.PublishAsync(new LeaveRequestCreatedEto
                {
                    LeaveRequestId = eventData.LeaveRequestId,
                    StaffId = eventData.EmployeeId,
                    EmployeeName = eventData.EmployeeName,
                    Message = $"Yeni izin talebi sisteme eklendi. (Tip: {eventData.LeaveType})",
                    StartDate = eventData.StartDate,
                    EndDate = eventData.EndDate,
                    Reason = eventData.Reason ?? string.Empty
                });
            }
            // =========================================================================

            await _employeeLeavesCache.RemoveAsync($"EmployeeLeaves_{eventData.EmployeeId}");
            await _leaveBalanceCache.RemoveAsync($"leave_balance_{eventData.EmployeeId}");
        }

        
    }
}