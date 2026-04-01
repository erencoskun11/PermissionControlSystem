using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Models;
using PermissionControlSystem.Statistics.Dtos;
using Polly;
using Polly.Wrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.Services
{
    public class ElasticSearchService : IElasticSearchService, ITransientDependency
    {
        private readonly IOpenSearchClient _elasticClient;
        private readonly ILogger<ElasticSearchService> _logger;

        // 🔥 1. Polly'i dışarıdan istemek yerine içeride statik oluşturuyoruz
        private static readonly AsyncPolicyWrap _elasticPolicy = Policy.WrapAsync(
            Policy.Handle<Exception>().CircuitBreakerAsync(3, TimeSpan.FromSeconds(30)),
            Policy.Handle<Exception>().WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
        );

        public ElasticSearchService(IOpenSearchClient elasticClient,
            ILogger<ElasticSearchService> logger
          )
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }


        public virtual async Task IndexLeaveRequestAsync(LeaveIndexModel model, CancellationToken cancellationToken = default)
        {
            var indexName = "leave_request";

            // ZIRH 1: Index var mı kontrolü
            var existsResponse = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.Indices.ExistsAsync(indexName,d =>d,ct),cancellationToken);
            if (!existsResponse.Exists)
            {
                // ZIRH 2: Index oluşturma işlemi
                await _elasticPolicy.ExecuteAsync(async ct =>
                    await _elasticClient.Indices.CreateAsync(indexName, c => c.Map<LeaveIndexModel>(m => m.AutoMap()), ct),
                    cancellationToken);
            }

            // ZIRH 3: Asıl veriyi basma işlemi
            var response = await _elasticPolicy.ExecuteAsync(async ct =>
                await _elasticClient.IndexAsync(model, idx => idx
                    .Index(indexName)
                    .Id(model.Id.ToString())
                    .Refresh(Refresh.WaitFor)
                , ct),
                cancellationToken);

            if (!response.IsValid)
            {
                throw new Exception($"Elastic Kayıt hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task UpdateLeaveRequestEmployeeDetailsAsync(Guid employeeId, string newName, string newDeptName, CancellationToken cancellationToken = default)
        {
            var safeEmployeeName = string.IsNullOrWhiteSpace(newName) ? "Bilinmiyor" : newName.Trim();
            var safeDepartmentName = string.IsNullOrWhiteSpace(newDeptName) ? "Belirtilmemiş" : newDeptName.Trim();

            // ZIRH 4: UpdateByQuery çağrısı
            var response = await _elasticPolicy.ExecuteAsync(async ct =>
                await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                    .Index("leave_request")
                    .Query(q => q.Term(t => t.Field(f => f.EmployeeId).Value(employeeId)))
                    .Script(s => s
                        .Source("ctx._source.employeeName = params.newName; ctx._source.departmentName = params.newDeptName;")
                        .Params(p => p
                            .Add("newName", safeEmployeeName)
                            .Add("newDeptName", safeDepartmentName)
                        )
                    )
                    .Refresh(true)
                , ct),
                cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError($"[ELASTIC] leave_request employee+department cascade update başarısız. EmployeeId: {employeeId}, Hata: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task UpdateLeaveRequestDepartmentNameByEmployeeIdAsync(Guid employeeId, string newDepartmentName, CancellationToken cancellationToken = default)
        {
            var safeDepartmentName = string.IsNullOrWhiteSpace(newDepartmentName) ? "Belirtilmemiş" : newDepartmentName.Trim();

            // ZIRH 5: Department Name UpdateByQuery çağrısı
            var response = await _elasticPolicy.ExecuteAsync(async ct =>
                await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                    .Index("leave_request")
                    .Query(q => q.Term(t => t.Field(f => f.EmployeeId).Value(employeeId)))
                    .Script(s => s
                        .Source("ctx._source.departmentName = params.newDeptName;")
                        .Params(p => p.Add("newDeptName", safeDepartmentName))
                    )
                    .Refresh(true)
                , ct),
                cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError($"[ELASTIC] leave_request department cascade update başarısız. EmployeeId: {employeeId}, Hata: {response.ServerError?.Error?.Reason}");
            }
        }
        public async Task IndexDepartmentAsync(Guid id, string name, string description, CancellationToken cancellationToken = default)
        {
            // 1. Elasticsearch'e gönderilecek dokümanı hazırlıyoruz
            var document = new DepartmentIndexModel
            {
                Id = id,
                Name = name,
                Description = description ?? "",
                LastModificationTime = DateTime.UtcNow
            };
            // ZIRH: İndeksleme işlemini pipeline ile sarmalıyoruz
            var response = await _elasticPolicy.ExecuteAsync(async ct => 
            await _elasticClient.IndexAsync(document, idx => idx
                .Index("departments") 
                .Id(id.ToString())    
                .Refresh(Refresh.WaitFor),
                ct)
                ,cancellationToken
            );

            if (!response.IsValid)
            {
                throw new Exception($"Departman verisi Elasticsearch'e yazılamadı: {response.OriginalException?.Message}");
            }

        }
        public async Task<List<DepartmentIndexModel>> SearchDepartmentAsync(string keyword, CancellationToken cancellationToken = default)
        {
            // Eğer kullanıcı arama kutusunu boş bırakırsa, boş liste dön
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<DepartmentIndexModel>();
            }

            var response = await _elasticPolicy.ExecuteAsync (async ct => await _elasticClient.SearchAsync<DepartmentIndexModel>(s => s
                .Index("departments") // Hangi index'te arayacağız?
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(p => p.Name)
                            .Field(p => p.Description) // Hem isimde hem açıklamada ara
                        )
                        .Query(keyword)
                        .Fuzziness(Fuzziness.Auto) // "IT" yerine "TI" yazsa bile bulsun (Typo-Tolerant)
                        
                    )
                ),ct)
                ,cancellationToken
            );

            if (!response.IsValid)
            {
                // Gerçek bir projede burayı loglamak daha iyidir.
                throw new UserFriendlyException($"ELASTICSEARCH HATASI: {response.OriginalException?.Message} || {response.ServerError?.Error?.Reason}");
            }

            // Elasticsearch'ten dönen dokümanları (sonuçları) listeye çevirip gönderiyoruz
            return response.Documents.ToList();
        }

        public async Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // ZIRH: Silme işlemini pipeline ile sarmalıyoruz
            var response = await _elasticPolicy.ExecuteAsync(async ct =>
                await _elasticClient.DeleteAsync<DepartmentIndexModel>(id, d => d
                    .Index("departments")
                , ct),
                cancellationToken);

            // If it fails (and the error IS NOT simply because the index or document doesn't exist), we throw
            if (!response.IsValid && response.ServerError?.Error?.Type != "index_not_found_exception")
            {
                // In a real production scenario, you might just log this rather than throwing,
                // but throwing here helps us catch issues during development.
                throw new Exception($"Elasticsearch Delete Error: {response.OriginalException?.Message} || {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task IndexEmployeeAsync(Guid id, Guid departmentId, string departmentName, string fullName, string position, string email,CancellationToken cancellationToken = default)
        {
            var employeeIndexModel = new EmployeeIndexModel
            {
                Id = id,
                DepartmentId = departmentId,
                DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? "Belirtilmemiş" : departmentName.Trim(),
                FullName = string.IsNullOrWhiteSpace(fullName) ? "Bilinmiyor" : fullName.Trim(),
                Position = position,
                Email = email
            };

            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.IndexAsync(employeeIndexModel, idx => idx
                .Index("employees")
                .Id(id.ToString())
                .Refresh(Refresh.WaitFor)
                ,ct),cancellationToken
            );
        }

        // 2. YENİ METODU EKLE (EN ALTA)
        // 🔥 SENIOR MİMARİ: Bu metod, "Bana DepartmentId'yi ver, o departmandaki BÜTÜN çalışanların adını saniyeler içinde yeni isimle değiştireyim" der!
        // 🔥 SENIOR MİMARİ: Departman adı değiştiğinde hem personelleri hem de o personellerin geçmiş tüm izinlerindeki departman adlarını günceller!
        // 🔥 SENIOR MİMARİ: Departman adı değiştiğinde personelleri ve onların geçmiş TÜM izinlerini tek seferde ezer!
        public async Task UpdateEmployeeDepartmentNameAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. ADIM: Bu departmandaki personelleri bul
                var searchResponse =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<EmployeeIndexModel>(s => s
                    .Index("employees")
                    .Size(10000)
                    .Query(q => q.Term(t => t.Field(f => f.DepartmentId).Value(departmentId.ToString())))
                    
                ,ct), cancellationToken);

                var employees = searchResponse.Documents.ToList();
                if (!employees.Any())
                {
                    _logger.LogWarning($"[ELASTIC] {departmentId} ID'li departmanda güncellenecek personel bulunamadı.");
                    return;
                }

                // 2. ADIM: Personellerin (employees index) departman adını güncelle (ZIRH 3: Toplu Güncelleme/Bulk)
                foreach (var emp in employees) { emp.DepartmentName = newDepartmentName; }

                await _elasticPolicy.ExecuteAsync(async ct =>
                    await _elasticClient.BulkAsync(b => b
                        .Index("employees")
                        .UpdateMany(employees, (ud, emp) => ud.Id(emp.Id.ToString()).Doc(emp).DocAsUpsert(true))
                        .Refresh(OpenSearch.Net.Refresh.WaitFor)
                    , ct),
                    cancellationToken);

                // 3. ADIM: İSTATİSTİKLER İÇİN İZİNLERİ GÜNCELLE (ASIL ÇÖZÜM BURASI!)
                // Personellerin ID'lerini bir listeye alıyoruz
                var empIds = employees.Select(e => e.Id.ToString()).ToList();

                var leaveResponse = await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                    .Index("leave_request")
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                // ElasticSearch Index yapısına göre iki ihtimali de arıyoruz, kaçarı yok!
                                sh => sh.Terms(t => t.Field(f => f.EmployeeId).Terms(empIds)),
                                sh => sh.Terms(t => t.Field(f => f.EmployeeId.Suffix("keyword")).Terms(empIds))
                            )
                            .MinimumShouldMatch(1)
                        )
                    )
                    .Script(s => s
                        // ElasticCase duyarlılığına karşı iki alanı da mühürle
                        .Source("ctx._source.departmentName = params.newName; ctx._source.DepartmentName = params.newName;")
                        .Params(p => p.Add("newName", newDepartmentName))
                    )
                    .Refresh(true)
                    ,ct),cancellationToken
                );

                _logger.LogInformation($"[ELASTIC BAŞARILI] {employees.Count} personelin ve {leaveResponse.Updated} izin kaydının departman adı '{newDepartmentName}' yapıldı.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ELASTIC CASCADE ERROR] Senkronizasyon hatası: {ex.Message}");
            }
        }

        // 🔥 SENIOR MİMARİ: Departman ID'sini kullanarak o departmandaki TÜM izinlerin adını ışık hızında günceller!
        public async Task UpdateLeaveRequestDepartmentNameByDepartmentIdAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                    .Index("leave_request")
                    .Query(q => q
                        // 🔥 KESİN EŞLEŞME: Metin değil, Term ile tam Guid eşleşmesi arıyoruz
                        .Term(t => t.Field(f => f.DepartmentId).Value(departmentId.ToString()))
                    )
                    .Script(s => s
                        // Elastic Case-Sensitive olduğu için her iki ihtimali de eziyoruz
                        .Source("ctx._source.departmentName = params.newDeptName; ctx._source.DepartmentName = params.newDeptName;")
                        .Params(p => p.Add("newDeptName", newDepartmentName))
                    )
                    .Refresh(true)
                    ,ct),cancellationToken
                );

                if (!response.IsValid)
                {
                    _logger.LogError($"[ELASTIC CASCADE HATASI] İzin departmanları güncellenemedi: {response.ServerError?.Error?.Reason}");
                }
                else
                {
                    _logger.LogInformation($"[ELASTIC BAŞARILI] Departman ID {departmentId} olan {response.Updated} adet geçmiş izin kaydının departman adı '{newDepartmentName}' yapıldı.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ELASTIC C# UPDATE HATASI] {ex.Message}");
            }
        }
        public async Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.DeleteAsync<EmployeeIndexModel>(id, d => d
                .Index("employees")
                ,ct),cancellationToken
            );
        }

        public async Task<List<EmployeeDto>> SearchEmployeeAsync(string keyword, CancellationToken cancellationToken = default)
        {
            // 1. Arama kutusu boşsa, veritabanını hiç yormadan boş liste dönüyoruz
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<EmployeeDto>();
            }

            // 2. DİKKAT: Aramayı kaydettiğimiz model (EmployeeIndexModel) üzerinden yapıyoruz!
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<EmployeeIndexModel>(s => s
                .Index("employees")
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(p => p.FullName)
                            .Field(p => p.Position)
                            .Field(p => p.Email)
                            .Field(p => p.DepartmentName)
                        )
                        .Query(keyword)
                        .Fuzziness(Fuzziness.Auto) // Typo Toleransı
                    )
                )
                ,ct),cancellationToken
            );

            // 3. SENIOR DOKUNUŞU: Elastic'ten gelen Index Modellerini, DTO'ya çevirip dışarıya öyle veriyoruz (Mapping)
            var dtoList = response.Documents.Select(doc => new EmployeeDto
            {
                Id = doc.Id,
                UserId = doc.UserId,
                FullName = doc.FullName,
                Position = doc.Position,
                Email = doc.Email,
                DepartmentId = doc.DepartmentId,
                DepartmentName = doc.DepartmentName,
                FirstName = doc.FirstName,
                LastName = doc.LastName,
                PhoneNumber = doc.PhoneNumber
            }).ToList();

            return dtoList;
        }

        public async Task<List<LeaveIndexModel>> SearchLeaveRequestAsync(string keyword, CancellationToken cancellationToken = default)
        {
            var indexName = "leave_request"; // IndexLeaveRequestAsync metodunda kullandığın isimle aynı olmalı

            // 1. Kullanıcı boş arama yaparsa, Elastic'i hiç yormadan boş liste dön
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<LeaveIndexModel>();
            }

            // 2. Işık hızında arama (MultiMatch ile birden fazla kolonda arıyoruz)
            var response = await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index(indexName)
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(p => p.EmployeeName) // Personel adında ara
                            .Field(p => p.Description)  // İzin açıklamasında (Reason) ara
                        )
                        .Query(keyword)
                        .Fuzziness(Fuzziness.Auto) // 🔥 SENIOR DOKUNUŞU: Harf hatalarını tolere et!
                    )
                )
                ,ct),cancellationToken
            );

            if (!response.IsValid)
            {
                _logger.LogError($"Elasticsearch Arama Hatası: {response.OriginalException?.Message}");
                throw new Exception("Arama sırasında bir hata oluştu.");
            }

            // 3. Bulunan dokümanları (sonuçları) listeye çevirip gönder
            return response.Documents.ToList();
        }

        // 🔥 İKİNCİ ZİNCİR: Çalışan adı değiştiğinde, o çalışanın tüm izin kayıtlarındaki ismi Elastic'te güncelle!
        public async Task UpdateLeaveRequestEmployeeNameAsync(Guid employeeId, string newEmployeeName, CancellationToken cancellationToken = default)
        {
            // Not: LeaveIndexModel içinde EmployeeId alanın yoksa, modeline public Guid EmployeeId { get; set; } eklemelisin!
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                .Index("leave_request")
                .Query(q => q
                    .Match(m => m.Field(f => f.EmployeeId).Query(employeeId.ToString())) // DİKKAT: Elastic modelinde EmployeeId varsa ona göre değiştir
                )
                .Script(s => s
                    .Source("ctx._source.employeeName = params.newName;")
                    .Params(p => p.Add("newName", newEmployeeName))
                )
                .Refresh(true)
                ,ct),cancellationToken
                
            );

            if (!response.IsValid)
            {
                _logger.LogError($"Elasticsearch İzin İsim Güncelleme Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        // ==========================================================
        // 🔥 SENKRONİZASYON (BULK INDEX) METODLARI
        // ==========================================================

        public async Task BulkIndexDepartmentsAsync(List<DepartmentIndexModel> departments, CancellationToken cancellationToken = default)
        {
            if (departments == null || !departments.Any()) return;

            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.BulkAsync(b => b
                .Index("departments")
                .IndexMany(departments, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor) // İşlem bitince hemen aranabilir olsun
                ,ct),cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu Departman Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task BulkIndexEmployeesAsync(List<EmployeeIndexModel> employees, CancellationToken cancellationToken = default)
        {
            if (employees == null || !employees.Any()) return;

            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.BulkAsync(b => b
                .Index("employees")
                .IndexMany(employees, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor)
                ,ct),cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu Çalışan Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task BulkIndexLeaveRequestsAsync(List<LeaveIndexModel> leaveRequests, CancellationToken cancellationToken = default)
        {
            if (leaveRequests == null || !leaveRequests.Any()) return;

            var response =await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.BulkAsync(b => b
                .Index("leave_request")
                .IndexMany(leaveRequests, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor)
                ,ct),cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu İzin Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }


        public async Task<List<LeaveTypeStatDto>> GetLeaveTypeStatsFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
            .Index("leave_request")
            .Size(0)
            .Aggregations(a => a
            .Terms("tiplere_gore_grupla", t => t
            .Field(f => f.LeaveType)
            )
            )
            ,ct),cancellationToken
            );

            // Dönen sadece sayılardır, çok hafiftir!
            var buckets = response.Aggregations.Terms("tiplere_gore_grupla").Buckets;

            return buckets.Select(b => new LeaveTypeStatDto
            {
                LeaveTypeName = ((LeaveType)int.Parse(b.Key)).ToString(),
                TotalCount = (int)b.DocCount
            }).ToList();


        }


        // 1. GENEL ÖZET
        public async Task<StatisticsOverviewDto> GetOverviewFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Aggregations(a => a
                    .Terms("status_counts", t => t.Field(f => f.Status))
                    .Filter("total_approved_days", f => f
                        .Filter(fi => fi.Term(t => t.Field(field => field.Status).Value((int)LeaveRequestStatus.Approved)))
                        // 🔥 SİHİRLİ DOKUNUŞ: Aggregations, Filter metodunun İÇİNE alındı!
                        .Aggregations(childAggs => childAggs
                            .Sum("sum_days", sum => sum.Field(field => field.DurationDays))
                        )
                    )
                )
            ,ct), cancellationToken);

            var result = new StatisticsOverviewDto();
            if (response.Aggregations == null) return result;

            var statusBuckets = response.Aggregations.Terms("status_counts")?.Buckets;
            result.TotalRequests = (int)response.Total;
            result.ApprovedRequests = (int)(statusBuckets?.FirstOrDefault(b => b.Key == ((int)LeaveRequestStatus.Approved).ToString())?.DocCount ?? 0);
            result.RejectedRequests = (int)(statusBuckets?.FirstOrDefault(b => b.Key == ((int)LeaveRequestStatus.Rejected).ToString())?.DocCount ?? 0);

            var approvedAgg = response.Aggregations.Filter("total_approved_days");
            if (approvedAgg != null && approvedAgg.Sum("sum_days") != null)
            {
                result.TotalLeaveDays = (int)(approvedAgg.Sum("sum_days").Value ?? 0);
            }

            return result;
        }

        // 2. DEPARTMAN BAZLI İSTATİSTİK
        public async Task<List<DepartmentLeaveStatDto>> GetDepartmentStatsFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Aggregations(a => a
                    .Terms("depts", t => t
                        .Field(f => f.DepartmentName.Suffix("keyword"))
                        // 🔥 SİHİRLİ DOKUNUŞ: Aggregations, Terms metodunun İÇİNE alındı!
                        .Aggregations(childAggs => childAggs
                            .Sum("dept_sum_days", sum => sum.Field(f => f.DurationDays))
                        )
                    )
                )
            ,ct), cancellationToken);

            var list = new List<DepartmentLeaveStatDto>();
            if (response.Aggregations == null) return list;

            foreach (var bucket in response.Aggregations.Terms("depts").Buckets)
            {
                list.Add(new DepartmentLeaveStatDto
                {
                    DepartmentName = bucket.Key,
                    TotalRequests = (int)bucket.DocCount,
                    TotalDays = (int)(bucket.Sum("dept_sum_days")?.Value ?? 0)
                });
            }

            return list;
        }

        // ==========================================
        // 🔥 YENİ SENIOR METODLAR (ELASTICSEARCH)
        // ==========================================

        // 1. En Çok İzin Kullanan 5 Personel
     
        
        // 3. Aylık İzin Dağılımı (Grafik için)
        public async Task<List<MonthlyLeaveStatDto>> GetMonthlyLeavesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Query(q => q.Bool(b => b.Must(
                    m => m.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Approved)),
                    m => m.DateRange(r => r.Field(f => f.StartDate).GreaterThanOrEquals(new DateTime(DateTime.Now.Year, 1, 1)))
                )))
                .Aggregations(a => a
                    .DateHistogram("monthly_stats", d => d.Field(f => f.StartDate).CalendarInterval(DateInterval.Month)
                        .Aggregations(aa => aa.Sum("days_sum", sum => sum.Field(f => f.DurationDays)))
                    )
                )
            ,ct), cancellationToken);

            return response.Aggregations.DateHistogram("monthly_stats").Buckets.Select(b => new MonthlyLeaveStatDto
            {
                MonthName = b.Date.ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                TotalDays = (int)(b.Sum("days_sum").Value ?? 0)
            }).ToList();
        }

        // 4. En Eski 5 Bekleyen Talep
        public async Task<List<OldestPendingLeaveStatDto>> GetOldestPendingLeavesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Pending)))
                .Sort(sort => sort.Ascending(f => f.CreationTime))
                .Size(5)
            ,ct),cancellationToken);

            return response.Documents.Select(d => new OldestPendingLeaveStatDto
            {
                EmployeeName = d.EmployeeName,
                LeaveTypeName = ((LeaveType)d.LeaveType).ToString(),
                CreatedDate = d.CreationTime.ToString("dd.MM.yyyy"),
                WaitingDays = (DateTime.Now - d.CreationTime).Days
            }).ToList();
        }

        public async Task DeleteLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Elasticsearch üzerindeki 'leave_request' indeksinden bu ID'yi uçuruyoruz
            var response =await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.DeleteAsync<LeaveIndexModel>(id, d => d.Index("leave_request"),ct),cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError($"[ELASTIC] İzin silinirken hata oluştu: {response.OriginalException.Message}");
            }
        }

        // 1. En Çok İzin Kullanan 5 Personel
        public async Task<List<TopEmployeeStatDto>> GetTopEmployeesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Approved)))
                .Aggregations(a => a
                    .Terms("per_employee", t => t
                        .Field(f => f.EmployeeName.Suffix("keyword"))
                        .Size(5)
                        .Aggregations(aa => aa
                            .Sum("total_days", sum => sum.Field(f => f.DurationDays))
                            // 🔥 DİKKAT: C# NEST için doğru syntax budur!
                            .TopHits("employee_details", th => th
                                .Size(1)
                                .Source(src => src.Includes(i => i.Field(f => f.DepartmentName)))
                            )
                        )
                    )
                )
            ,ct), cancellationToken);

            if (!response.IsValid || response.Aggregations == null) return new List<TopEmployeeStatDto>();

            return response.Aggregations.Terms("per_employee").Buckets.Select(b =>
            {
                // TopHits'ten departman adını çekiyoruz
                var firstHit = b.TopHits("employee_details")?.Documents<LeaveIndexModel>().FirstOrDefault();
                string deptName = firstHit?.DepartmentName;

                // Boş veya null gelirse '-' koy ki JS düzgün anlasın
                if (string.IsNullOrWhiteSpace(deptName)) deptName = "-";

                return new TopEmployeeStatDto
                {
                    EmployeeName = b.Key,
                    RequestCount = (int)b.DocCount,
                    TotalDays = (int)(b.Sum("total_days")?.Value ?? 0),
                    DepartmentName = deptName
                };
            }).OrderByDescending(x => x.TotalDays).ToList();
        }

        // 2. En Çok Reddedilen 5 Personel
        public async Task<List<RejectedEmployeeStatDto>> GetMostRejectedEmployeesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response =await _elasticPolicy.ExecuteAsync(async ct=> await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Rejected)))
                .Aggregations(a => a
                    .Terms("rejected_employees", t => t
                        .Field(f => f.EmployeeName.Suffix("keyword"))
                        .Size(5)
                        .Aggregations(aa => aa
                            // 🔥 DİKKAT: C# NEST için doğru syntax budur!
                            .TopHits("employee_details", th => th
                                .Size(1)
                                .Source(src => src.Includes(i => i.Field(f => f.DepartmentName)))
                            )
                        )
                    )
                )
            ,ct), cancellationToken);

            if (!response.IsValid || response.Aggregations == null) return new List<RejectedEmployeeStatDto>();

            return response.Aggregations.Terms("rejected_employees").Buckets.Select(b =>
            {
                // TopHits'ten departman adını çekiyoruz
                var firstHit = b.TopHits("employee_details")?.Documents<LeaveIndexModel>().FirstOrDefault();
                string deptName = firstHit?.DepartmentName;

                // Boş veya null gelirse '-' koy ki JS düzgün anlasın
                if (string.IsNullOrWhiteSpace(deptName)) deptName = "-";

                return new RejectedEmployeeStatDto
                {
                    EmployeeName = b.Key,
                    RejectCount = (int)b.DocCount,
                    DepartmentName = deptName
                };
            }).ToList();
        }
    }

}