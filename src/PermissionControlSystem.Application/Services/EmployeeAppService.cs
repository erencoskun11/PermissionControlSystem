using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Employees;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Events.Employees;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Outbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core.Tokenizer;
using System.Text.Json;
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
using Volo.Abp.Uow;
namespace PermissionControlSystem.Services
{
    [RemoteService(IsEnabled = false)]
    public class EmployeeAppService :
        CrudAppService<
            Employee,
            EmployeeDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateEmployeeDto,
            UpdateEmployeeDto>,
        IEmployeeAppService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly EmployeeManager _employeeManager;
        private readonly IDistributedCache<List<EmployeeCacheItem>, string> _employeeListCache;
        private readonly IDistributedCache<EmployeeCacheItem, string> _singleEmployeeCache;
        private readonly ILocalEventBus _localEventBus;
        public EmployeeAppService(
            IEmployeeRepository employeeRepository,
            EmployeeManager employeeManager,
            ILocalEventBus localEventBus,
            IElasticSearchService elasticSearchService,
            IDistributedCache<List<EmployeeCacheItem>,string> emploeeListCache,
            IDistributedCache<EmployeeCacheItem,string> singleEmployeeCache)
            : base(employeeRepository)
        {
            _employeeRepository = employeeRepository;
           _elasticSearchService = elasticSearchService;
            _singleEmployeeCache = singleEmployeeCache;
            _employeeListCache = emploeeListCache;
            _employeeManager = employeeManager;
            _localEventBus = localEventBus;
        }
        public async Task<List<EmployeeCacheItem>> GetCachedEmployeeListAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return await _employeeListCache.GetOrAddAsync(
                "AllActiveEmployees",
                async () =>  await GetEmployeesFromDbAndMapAsync(cancellationToken), 
                () => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2) },
                token : cancellationToken
            );
#pragma warning restore CS8603 // Possible null reference return.
        }



        private async Task<List<EmployeeCacheItem>> GetEmployeesFromDbAndMapAsync(CancellationToken cancellationToken = default)
        {
            // Departman (Join) ile birlikte veriyi çekiyoruz
            var query = await _employeeRepository.WithDetailsAsync(e => e.Department);
            var employees = await AsyncExecuter.ToListAsync(query,cancellationToken);

            // Entity -> CacheItem dönüşümünü burada yapıyoruz
            return employees.Select(e => new EmployeeCacheItem(
                e.Id, e.UserId, e.DepartmentId, e.FirstName, e.LastName,
                $"{e.FirstName} {e.LastName}", e.Position, e.Email, e.PhoneNumber,
                e.Department?.Name ?? "Departman Atanmamış",
                e.ConcurrencyStamp
            )).ToList();
        }


        public override async Task<PagedResultDto<EmployeeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            // 🔥 override metotlarda parametre gelmediği için sağlayıcıdan alıyoruz
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;

            // Veritabanına gitmek yerine Cache'teki listeyi çekiyoruz
            var cachedEmployees = await GetCachedEmployeeListAsync(token);

            var totalCount = cachedEmployees.Count;

            // Sayfalama (Pagination)
            var pagedItems = cachedEmployees
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            return new PagedResultDto<EmployeeDto>(
                totalCount,
                ObjectMapper.Map<List<EmployeeCacheItem>, List<EmployeeDto>>(pagedItems)
            );
        }

        // 🟢 3. TEKİL CACHE OVERRIDE (Detay sayfası için ışık hızında getirme)
        public override async Task<EmployeeDto> GetAsync(Guid id)
        {
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
            var cacheKey = $"Employee_{id}";

            var cacheItem = await _singleEmployeeCache.GetOrAddAsync(
                cacheKey,
                async () =>
                {
                    var entity = await _employeeRepository.GetWithDetailsAsync(id,token);

                    return new EmployeeCacheItem(
                        entity.Id,
                        entity.UserId,
                        entity.DepartmentId,
                        entity.FirstName,
                        entity.LastName,
                        $"{entity.FirstName} {entity.LastName}",
                        entity.Position,
                        entity.Email,
                        entity.PhoneNumber,
                        entity.Department?.Name,
                        entity.ConcurrencyStamp
                    );
                },
                () => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }
            );

            return ObjectMapper.Map<EmployeeCacheItem, EmployeeDto>(cacheItem);
        }

        public override async Task<EmployeeDto> CreateAsync(CreateEmployeeDto input)
        {
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
            // 🔥 SENIOR DOKUNUŞU: new Employee() DİYE KENDİMİZ OLUŞTURMUYORUZ!
            // İşi uzmanına (Manager'a) bırakıyoruz ki "Aynı e-posta var mı?" diye kontrol etsin.
            var entity = await _employeeManager.CreateAsync(
                input.UserId,
                input.DepartmentId,
                input.FirstName,
                input.LastName,
                input.Email,
                input.PhoneNumber,
                input.Position ?? ""
            );

            // 2. Veritabanına Kaydet
            // 🔥 SİHİR BURADA: ABP bu satırda otomatik olarak EntityCreatedEventData fırlatacak!
            await _employeeRepository.InsertAsync(entity, autoSave: true,cancellationToken: token );


            // DTO'yu dön, manuel event fırlatma AMELELİĞİNE SON!
            return ObjectMapper.Map<Employee, EmployeeDto>(entity);
        
        
        }
      
        public override async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto input)
        {
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
            var entity = await _employeeRepository.GetAsync(id,cancellationToken : token);

            Check.NotNullOrWhiteSpace(input.ConcurrencyStamp, nameof(input.ConcurrencyStamp));

            // 🔥 3. MADDE ENTEGRASYONU: Mühür Tokuşturma
            // Ekrandan (input) gelen mührü, veritabanından gelen entity'ye basıyoruz.
            // Eğer bu mühür veritabanındakiyle eşleşmezse, aşağıdaki UpdateAsync satırında 
            // ABP otomatik olarak ConcurrencyException fırlatacaktır.
            entity.ConcurrencyStamp = input.ConcurrencyStamp;

            // 🔥 Manager üzerinden güncelliyoruz
            await _employeeManager.UpdateAsync(
                entity, input.DepartmentId, input.FirstName, input.LastName,
                input.Email, input.PhoneNumber, input.Position ?? ""
            );

            // 🔥 SİHİR BURADA: ABP EntityUpdatedEvent fırlatacak
            await _employeeRepository.UpdateAsync(entity, autoSave: true, cancellationToken: token);


            // 🔥 KRİTİK NOKTA: Cevap dönmeden önce Departman detaylarını yükle!
            // Eğer bunu yapmazsan, ObjectMapper departman adını boş görür.
            var updatedEntityWithDetails = await _employeeRepository.GetWithDetailsAsync(id,token);

            return ObjectMapper.Map<Employee, EmployeeDto>(updatedEntityWithDetails);
        }

        public override async Task DeleteAsync(Guid id)
        {
            var token = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
            await _employeeRepository.DeleteAsync(id, autoSave: true, cancellationToken: token);
        }

        [HttpGet("search")]
        public async Task<List<EmployeeDto>> SearchAsync(string keyword,CancellationToken cancellationToken =default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<EmployeeDto>();
            }

            try
            {
                // 🔥 SENIOR DÜZELTME: ElasticSearchService zaten her şeyi (DepartmentName dahil) 
                // mükemmel şekilde DTO'ya çeviriyor. Bizim burada tekrar parçalayıp bozmamıza gerek yok!
                // Sadece gelen veriyi doğrudan dışarı yolluyoruz.
                return await _elasticSearchService.SearchEmployeeAsync(keyword,cancellationToken);
            }
            catch (Exception ex)
            {
                // 1. Logger kontrolü: Eğer ApplicationService'den türediyse Logger çalışır.
                Logger.LogError($"Elasticsearch arama hatası: {ex.Message}. Veritabanından aranıyor...");

                // 2. FALLBACK OPERASYONU: Queryable üzerinden güvenli filtreleme
                // Önce Queryable nesnesini alıyoruz
                var query = await _employeeRepository.GetQueryableAsync();

                // Filtremizi uyguluyoruz ve AsyncExecuter ile telsiz (token) eşliğinde listeye çeviriyoruz
                var dbItems = await AsyncExecuter.ToListAsync(
                    query.Where(e =>
                        e.FirstName.Contains(keyword) ||
                        e.LastName.Contains(keyword) ||
                        e.Position.Contains(keyword)
                    ),
                    cancellationToken // 🔥 Telsiz burada çok önemli!
                );

                // 3. Dönüşüm: Entity listesini DTO listesine çevir
                return ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(dbItems);
            }
        }


        [HttpPost("bulk-create-employee")]
        public virtual async Task BulkCreateAsync([FromBody]List<CreateEmployeeDto> input, CancellationToken cancellationToken = default)
        {
            var employees = new List<Employee>();

            // 1. AŞAMA: İŞ KURALLARI (Domain Logic)
            // Doğrudan veritabanına basmıyoruz, EmployeeManager'dan geçiriyoruz ki "Bu e-posta var mı?" kontrolü yapılsın.
            foreach (var dto in input)
            {
                // 🔥 DÖNGÜ İÇİNDE KRİTİK KONTROL: Komutan "Dur" dediyse hemen dur!
                cancellationToken.ThrowIfCancellationRequested();
                var entity = await _employeeManager.CreateAsync(
                    dto.UserId,
                    dto.DepartmentId,
                    dto.FirstName,
                    dto.LastName,
                    dto.Email,
                    dto.PhoneNumber,
                    dto.Position ?? ""
                );
                employees.Add(entity);
            }
            // 2. AŞAMA: VERİTABANINA TOPLU KAYIT (Bulk Insert)
            // Döngü içinde değil, tek seferde hepsini yazar. Performansı uçurur!
            await _employeeRepository.InsertManyAsync(employees, autoSave: true,cancellationToken);

            await _localEventBus.PublishAsync(new EmployeesBulkCreatedEvent
            {
                Employees = employees
            });
                       
        }
    }
}