using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Events.Department;
using PermissionControlSystem.Events.Employees;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Threading;
using static PermissionControlSystem.Permissions.PermissionControlSystemPermissions;

namespace PermissionControlSystem.Departments
{
    [RemoteService(IsEnabled = false)] // 🔥 SİHİRLİ DOKUNUŞ: ABP'nin otomatik API'sini kapatır!
    public class DepartmentAppService :
        CrudAppService<
            Department,
            DepartmentDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateDepartmentDto,
            UpdateDepartmentDto>,
        IDepartmentAppService
    {
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IElasticSearchService _elasticsearchService;
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly DepartmentManager _departmentManager;
        private readonly IDistributedCache<List<DepartmentCacheItem>, string> _departmentCache;
        private readonly ILocalEventBus _localEventBus;
        private readonly IRepository<Employee, Guid> _employeeRepository;

        public DepartmentAppService(
            IDepartmentRepository departmentRepository,
            IElasticSearchService elasticSearchService,
            DepartmentManager departmentManager,
            IDistributedCache<List<DepartmentCacheItem>, string> departmentCache,
            ILocalEventBus localEventBus,
            IRepository<Employee, Guid> employeeRepository,
            IDistributedEventBus distributedEventBus)
            : base(departmentRepository)
        {
            _departmentRepository = departmentRepository;
            _elasticsearchService = elasticSearchService;
            _departmentManager = departmentManager;
            _departmentCache = departmentCache;
            _localEventBus = localEventBus;
            _employeeRepository = employeeRepository;
            _distributedEventBus = distributedEventBus;
        }

        public async Task<List<DepartmentCacheItem>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CS8603 
            return await _departmentCache.GetOrAddAsync(
                "AllActiveDepartments",
                async () =>
                {
                    var departments = await _departmentRepository.GetListAsync(cancellationToken: cancellationToken);

                    return departments.Select(d => new DepartmentCacheItem(
                        d.Id,
                        d.Name ?? string.Empty,
                        d.Description ?? string.Empty
                    )).ToList()!;
                },
                () => new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                },
                token: cancellationToken // 🔥 Redis I/O işlemi için telsiz eklendi
            );
#pragma warning restore CS8603
        }

        public override async Task<PagedResultDto<DepartmentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            // 🔥 FOOLPROOF ÇÖZÜM: ABP'nin LazyServiceProvider'ı üzerinden telsizi %100 garantili çekiyoruz!
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;

            var departmentQuery = await Repository.GetQueryableAsync();
            var employeeQuery = await _employeeRepository.GetQueryableAsync();

            var totalCount = await AsyncExecuter.CountAsync(departmentQuery, cancellationToken: token);

            var pagedDepartments = await AsyncExecuter.ToListAsync(
                departmentQuery
                    .OrderBy(x => x.Name)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount),
                cancellationToken: token // 👈 Telsiz devrede
            );

            // (Silinen yerler geri geldi)
            var departmentIds = pagedDepartments.Select(x => x.Id).ToList();

            var employeeCounts = await AsyncExecuter.ToListAsync(
                employeeQuery
                    .Where(x => departmentIds.Contains(x.DepartmentId))
                    .GroupBy(x => x.DepartmentId)
                    .Select(g => new
                    {
                        DepartmentId = g.Key,
                        Count = g.Count()
                    }),
                cancellationToken: token // 👈 Telsiz devrede
            );

            var employeeCountDict = employeeCounts.ToDictionary(x => x.DepartmentId, x => x.Count);

            var items = pagedDepartments.Select(department => new DepartmentDto
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                EmployeeCount = employeeCountDict.TryGetValue(department.Id, out var count) ? count : 0
            }).ToList();

            // (Silinen return geri geldi)
            return new PagedResultDto<DepartmentDto>(
                totalCount,
                items
            );
        }

        
        public override async Task<DepartmentDto> CreateAsync(CreateDepartmentDto input)
        {
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
            var department = await _departmentManager.CreateAsync(input.Name, input.Description ?? "");

            // 1. Veritabanına kaydet
            await _departmentRepository.InsertAsync(department, autoSave: true, cancellationToken: token);

            await _localEventBus.PublishAsync(new DepartmentCreatedEvent
            {
                DepartmentId = department.Id,
                DepartmentName = department.Name,
                Description = department.Description
            });

            return ObjectMapper.Map<Department, DepartmentDto>(department);
        }

        public override async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentDto input)
        {
            // 🔥 FOOLPROOF ÇÖZÜM: Telsizi burada da yakalıyoruz
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;

            // 1. Önce güncellenecek nesneyi veritabanından çek (Telsiz 'token' olarak paslandı)
            var department = await _departmentRepository.GetAsync(id, cancellationToken: token);

            department.ConcurrencyStamp = input.ConcurrencyStamp;

            // 🔥 SENIOR DOKUNUŞU: İsim değişikliğini ve kontrolünü Manager yapıyor!
            await _departmentManager.ChangeNameAsync(department, input.Name);

            department.SetDescription(input.Description);

            // 1. ADIM: SQL Güncellemesi (Telsiz 'token' olarak paslandı)
            await _departmentRepository.UpdateAsync(department, autoSave: true, cancellationToken: token);

            return ObjectMapper.Map<Department, DepartmentDto>(department);
        }

        public async Task<List<DepartmentDto>> SearchFromElasticAsync(string keyword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return new List<DepartmentDto>();

            // 🔥 ElasticSearch I/O işlemi için telsiz eklendi
            var searchResults = await _elasticsearchService.SearchDepartmentAsync(keyword, cancellationToken);

            // 🔥 SENIOR FIX: IndexModel'i doğrudan UI'a açma, DTO'ya çevir!
            return searchResults.Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description
            }).ToList();
        }

        public override async Task DeleteAsync(Guid id)
        {
            await _departmentManager.CheckCanDeleteAsync(id);
            // Kuralı geçerse standart silme işlemine devam et (base kendisi CancellationToken kullanır)
            await base.DeleteAsync(id);
        }

        public virtual async Task BulkCreateAsync([FromBody] List<CreateDepartmentDto> input, CancellationToken cancellationToken = default)
        {
            var departments = new List<Department>();

            foreach (var dto in input)
            {
                // 🔥 SİHİRLİ DOKUNUŞ: Eğer toplu işlem sırasında komutan (kullanıcı) işlemi iptal ederse, 
                // döngü bir sonraki adımı beklemeden anında Exception fırlatıp işlemi keser!
                cancellationToken.ThrowIfCancellationRequested();

                var entity = await _departmentManager.CreateAsync(
                    dto.Name,
                    dto.Description ?? ""
                );
                departments.Add(entity);
            }

            // 2. SQL'E TOPLU KAYIT (Telsiz eklendi)
            await _departmentRepository.InsertManyAsync(departments, autoSave: true, cancellationToken: cancellationToken);

            await _localEventBus.PublishAsync(new DepartmentsBulkCreatedEvent
            {
                Departments = departments
            });
        }
    }
}