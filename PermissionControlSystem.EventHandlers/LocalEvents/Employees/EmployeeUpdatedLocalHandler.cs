using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Outbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Guids;

namespace PermissionControlSystem.EventHandlers
{
    public class EmployeeUpdatedLocalHandler : ILocalEventHandler<EntityUpdatedEventData<Employee>>, ITransientDependency
    {
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IDistributedCache<List<EmployeeCacheItem>, string> _employeeListCache;
        private readonly IDistributedCache<EmployeeCacheItem, string> _singleEmployeeCache;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCache;
        private readonly IGuidGenerator _guidGenerator; // 🔥 Eklendi

        public EmployeeUpdatedLocalHandler(
            IRepository<OutboxMessage, Guid> outboxRepository,
            IRepository<Employee, Guid> employeeRepository,
            IDistributedCache<List<EmployeeCacheItem>, string> employeeListCache,
            IDistributedCache<EmployeeCacheItem, string> singleEmployeeCache,
            IDistributedCache<List<LeaveRequestCacheItem>, string> employeeLeavesCache,
            IGuidGenerator guidGenerator) // 🔥 Inject edildi
        {
            _outboxRepository = outboxRepository;
            _employeeRepository = employeeRepository;
            _employeeListCache = employeeListCache;
            _singleEmployeeCache = singleEmployeeCache;
            _employeeLeavesCache = employeeLeavesCache;
            _guidGenerator = guidGenerator;
        }

        public async Task HandleEventAsync(EntityUpdatedEventData<Employee> eventData)
        {
            var entity = eventData.Entity;
            var detailedEmployee = await _employeeRepository.GetAsync(entity.Id, includeDetails: true);
            var departmentName = string.IsNullOrWhiteSpace(detailedEmployee.Department?.Name)
                ? "Belirtilmemiş"
                : detailedEmployee.Department.Name;

            // 1. OUTBOX KAYDI (Sözleşmeye Uygun - Güncel Versiyon)
            await _outboxRepository.InsertAsync(new OutboxMessage(
                _guidGenerator.Create(), // ✅ Guid.NewGuid() yerine ABP standardı
                "Employee",              // ✅ "EmployeeUpdated" yerine sabit "Employee" (Factory için)
                JsonSerializer.Serialize(new
                {
                    Action = "Updated",  // ✅ Worker'ın ne yapacağını anlaması için şart!
                    EmployeeId = detailedEmployee.Id,
                    FullName = $"{detailedEmployee.FirstName} {detailedEmployee.LastName}",
                    Position = detailedEmployee.Position,
                    Email = detailedEmployee.Email,
                    DepartmentId = detailedEmployee.DepartmentId,
                    DepartmentName = departmentName
                })
            ));

            // 2. ZEKİ CACHE GÜNCELLEME (Burası zaten harikaydı, dokunmadık)
            var cachedEmployees = await _employeeListCache.GetAsync("AllActiveEmployees");
            if (cachedEmployees != null)
            {
                var targetEmployee = cachedEmployees.FirstOrDefault(x => x.Id == entity.Id);
                if (targetEmployee != null)
                {
                    targetEmployee.FirstName = detailedEmployee.FirstName;
                    targetEmployee.LastName = detailedEmployee.LastName;
                    targetEmployee.FullName = $"{detailedEmployee.FirstName} {detailedEmployee.LastName}";
                    targetEmployee.Position = detailedEmployee.Position;
                    targetEmployee.Email = detailedEmployee.Email;
                    targetEmployee.PhoneNumber = detailedEmployee.PhoneNumber;
                    targetEmployee.DepartmentName = departmentName;
                    targetEmployee.DepartmentId = detailedEmployee.DepartmentId;

                    await _employeeListCache.SetAsync("AllActiveEmployees", cachedEmployees);
                }
            }

            // 3. TEMİZLİK
            await _singleEmployeeCache.RemoveAsync($"Employee_{entity.Id}");
            await _employeeLeavesCache.RemoveAsync($"EmployeeLeaves_{entity.Id}");
        }
    }
}