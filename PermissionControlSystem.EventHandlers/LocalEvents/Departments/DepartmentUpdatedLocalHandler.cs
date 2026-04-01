using PermissionControlSystem.Caching;
using PermissionControlSystem.Entities;
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
using Volo.Abp.Guids; // 🔥 IGuidGenerator için şart

namespace PermissionControlSystem.EventHandlers.LocalEvents.Departments
{
    public class DepartmentUpdatedLocalHandler : ILocalEventHandler<EntityUpdatedEventData<Department>>, ITransientDependency
    {
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
        private readonly IDistributedCache<List<DepartmentCacheItem>, string> _departmentCache;
        private readonly IDistributedCache<List<EmployeeCacheItem>, string> _employeeListCache;
        private readonly IDistributedCache<EmployeeCacheItem, string> _singleEmployeeCache; // 🔥 Eklendi
        private readonly IRepository<Employee, Guid> _employeeRepository; // 🔥 Eklendi
        private readonly IGuidGenerator _guidGenerator; // 🔥 Eklendi

        public DepartmentUpdatedLocalHandler(
            IRepository<OutboxMessage, Guid> outboxRepository,
            IDistributedCache<List<DepartmentCacheItem>, string> departmentCache,
            IDistributedCache<List<EmployeeCacheItem>, string> employeeListCache,
            IDistributedCache<EmployeeCacheItem, string> singleEmployeeCache, // 🔥 Eklendi
            IRepository<Employee, Guid> employeeRepository, // 🔥 Eklendi
            IGuidGenerator guidGenerator) // 🔥 Inject edildi
        {
            _outboxRepository = outboxRepository;
            _departmentCache = departmentCache;
            _employeeListCache = employeeListCache;
            _singleEmployeeCache = singleEmployeeCache; // 🔥 Eklendi
            _employeeRepository = employeeRepository; // 🔥 Eklendi
            _guidGenerator = guidGenerator;
        }

        public async Task HandleEventAsync(EntityUpdatedEventData<Department> eventData)
        {
            var dept = eventData.Entity;

            // 1. OUTBOX: Elasticsearch/OpenSearch Sözleşmesine Uygun Kayıt
            await _outboxRepository.InsertAsync(new OutboxMessage(
                _guidGenerator.Create(), // ✅ ABP Standardı
                "Department",            // ✅ Tip sabitlendi (Factory için)
                JsonSerializer.Serialize(new
                {
                    Action = "Updated",  // ✅ Worker artık bu "Action"a bakacak
                    Id = dept.Id,
                    Name = dept.Name,
                    Description = dept.Description
                })
            ));

            // 2. CACHE: Departman listesi değiştiği için temizliyoruz
            await _departmentCache.RemoveAsync("AllActiveDepartments");

            // 3. CASCADE CACHE INVALIDATION: 
            // Departman adı değiştiği için çalışan listesindeki "DepartmentName" alanları bayatladı!
            // O yüzden çalışan listesini de patlatıyoruz.
            await _employeeListCache.RemoveAsync("AllActiveEmployees");

            // 🔥 4. TEKİL ÇALIŞAN CACHE'LERİNİ TEMİZLE
            var employeeIds = await _employeeRepository.GetListAsync(x => x.DepartmentId == dept.Id);
            foreach (var emp in employeeIds)
            {
                await _singleEmployeeCache.RemoveAsync($"Employee_{emp.Id}");
            }
        }
    }
}