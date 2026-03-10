using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Salarys.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Models;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace PermissionControlSystem.Salarys
{
    [RemoteService(IsEnabled = false)]
    [Authorize]
    public class SalaryAppService :
        CrudAppService<
            Salary,
            SalaryDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateSalaryDto,
            UpdateSalaryDto>,
        ISalaryAppService
    {
        private readonly ISalaryRepository _salaryRepository;
        private readonly INotificationService _notificationService;
        private readonly IElasticSearchService _elasticsearchService;
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly SalaryManager _salaryManager;
        private readonly IDistributedCache<List<SalaryCacheItem>, string> _salaryCache;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepository;

        public SalaryAppService(
            ISalaryRepository salaryRepository,
            INotificationService notificationService,
            IElasticSearchService elasticSearchService,
            IDistributedEventBus distributedEventBus,
            SalaryManager salaryManager,
            IDistributedCache<List<SalaryCacheItem>, string> salaryCache,
            IRepository<OutboxMessage, Guid> outRepository) 
            : base(salaryRepository)
        {
            _salaryRepository = salaryRepository;
            _notificationService = notificationService;
            _elasticsearchService = elasticSearchService;
            _distributedEventBus = distributedEventBus;
            _salaryManager = salaryManager;
            _salaryCache = salaryCache;
            _outboxRepository = outRepository;
        }

        public async Task<List<SalaryCacheItem>> GetSalarysAsync()
        {
#pragma warning disable CS8603 
            return await _salaryCache.GetOrAddAsync(
                "AllActiveSalarys",
                async () =>
                {
                    var items = await _salaryRepository.GetListAsync();

                    return items.Select(d => new SalaryCacheItem(
                        d.Id,
                        d.Name ?? string.Empty,        
                        d.Description ?? string.Empty  
                    )).ToList()!;
                },
                () => new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                }
            );
#pragma warning restore CS8603 
        }

        public override async Task<PagedResultDto<SalaryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var cachedItems = await GetSalarysAsync();
            var totalCount = cachedItems.Count;
            var pagedItems = cachedItems.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

            return new PagedResultDto<SalaryDto>(
                totalCount,
                ObjectMapper.Map<List<SalaryCacheItem>, List<SalaryDto>>(pagedItems)
            );
        }

        [Authorize(Roles = "admin")]
        public override async Task<SalaryDto> CreateAsync(CreateSalaryDto input)
        {
            var salary = await _salaryManager.CreateAsync(input.Name, input.Description ?? "");
            await _salaryRepository.InsertAsync(salary, autoSave: true);

            var outboxMessage = new OutboxMessage(
                GuidGenerator.Create(),
                "SalaryCreated",
                JsonSerializer.Serialize(new { salary.Id, salary.Name, salary.Description })
            );
            await _outboxRepository.InsertAsync(outboxMessage);

            await _notificationService.AddNotificationAsync($"🏢 Yeni Salary: '{ salary.Name }' eklendi.");

            await _distributedEventBus.PublishAsync(new SalaryCreatedEto
            {
                SalaryId = salary.Id,
                SalaryName = salary.Name,
                Message = "Yeni kayıt başarılı!"
            });

            await _salaryCache.RemoveAsync("AllActiveSalarys");
            
            return ObjectMapper.Map<Salary, SalaryDto>(salary);
        }

        public Task<List<SalaryDto>> SearchFromElasticAsync(string keyword)
        {
            throw new NotImplementedException();
        }
    }
}