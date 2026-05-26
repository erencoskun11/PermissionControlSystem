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
using Polly.Registry;
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
        private readonly ElasticResilienceAdapter _elasticPolicy;

        public ElasticSearchService(IOpenSearchClient elasticClient,
            ILogger<ElasticSearchService> logger,
            ResiliencePipelineProvider<string> pipelineProvider)
        {
            _elasticClient = elasticClient;
            _logger = logger;
            _elasticPolicy = new ElasticResilienceAdapter(pipelineProvider.GetPipeline("elastic"));
        }

        private sealed class ElasticResilienceAdapter
        {
            private readonly ResiliencePipeline _pipeline;

            public ElasticResilienceAdapter(ResiliencePipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken = default)
            {
                return await _pipeline.ExecuteAsync(async token => await callback(token), cancellationToken);
            }
        }

        public virtual async Task IndexLeaveRequestAsync(LeaveIndexModel model, CancellationToken cancellationToken = default)
        {
            var indexName = "leave_request";

            // ZIRH 1: Index var mı kontrolü
            var existsResponse = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.Indices.ExistsAsync(indexName, d => d, ct), cancellationToken);
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
            var document = new DepartmentIndexModel
            {
                Id = id,
                Name = name,
                Description = description ?? "",
                LastModificationTime = DateTime.UtcNow
            };

            var response = await _elasticPolicy.ExecuteAsync(async ct =>
            await _elasticClient.IndexAsync(document, idx => idx
                .Index("departments")
                .Id(id.ToString())
                .Refresh(Refresh.WaitFor),
                ct)
                , cancellationToken
            );

            if (!response.IsValid)
            {
                throw new Exception($"Departman verisi Elasticsearch'e yazılamadı: {response.OriginalException?.Message}");
            }
        }

        public async Task<List<DepartmentIndexModel>> SearchDepartmentAsync(string keyword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<DepartmentIndexModel>();
            }

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<DepartmentIndexModel>(s => s
                .Index("departments")
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(p => p.Name)
                            .Field(p => p.Description)
                        )
                        .Query(keyword)
                        .Fuzziness(Fuzziness.Auto)
                    )
                ), ct)
                , cancellationToken
            );

            if (!response.IsValid)
            {
                throw new UserFriendlyException($"ELASTICSEARCH HATASI: {response.OriginalException?.Message} || {response.ServerError?.Error?.Reason}");
            }

            return response.Documents.ToList();
        }

        public async Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct =>
                await _elasticClient.DeleteAsync<DepartmentIndexModel>(id, d => d
                    .Index("departments")
                , ct),
                cancellationToken);

            if (!response.IsValid && response.ServerError?.Error?.Type != "index_not_found_exception")
            {
                throw new Exception($"Elasticsearch Delete Error: {response.OriginalException?.Message} || {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task IndexEmployeeAsync(Guid id, Guid departmentId, string departmentName, string fullName, string position, string email, CancellationToken cancellationToken = default)
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

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.IndexAsync(employeeIndexModel, idx => idx
                .Index("employees")
                .Id(id.ToString())
                .Refresh(Refresh.WaitFor)
                , ct), cancellationToken
            );
        }

        public async Task UpdateEmployeeDepartmentNameAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default)
        {
            try
            {
                var searchResponse = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<EmployeeIndexModel>(s => s
                    .Index("employees")
                    .Size(10000)
                    .Query(q => q.Term(t => t.Field(f => f.DepartmentId).Value(departmentId.ToString())))
                , ct), cancellationToken);

                var employees = searchResponse.Documents.ToList();
                if (!employees.Any())
                {
                    _logger.LogWarning($"[ELASTIC] {departmentId} ID'li departmanda güncellenecek personel bulunamadı.");
                    return;
                }

                foreach (var emp in employees) { emp.DepartmentName = newDepartmentName; }

                await _elasticPolicy.ExecuteAsync(async ct =>
                    await _elasticClient.BulkAsync(b => b
                        .Index("employees")
                        .UpdateMany(employees, (ud, emp) => ud.Id(emp.Id.ToString()).Doc(emp).DocAsUpsert(true))
                        .Refresh(OpenSearch.Net.Refresh.WaitFor)
                    , ct),
                    cancellationToken);

                _logger.LogInformation("[ELASTIC BAŞARILI] {EmployeeCount} personelin departman adı '{DepartmentName}' yapıldı.", employees.Count, newDepartmentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ELASTIC CASCADE ERROR] Employee index senkronizasyon hatası.");
                throw;
            }
        }

        public async Task UpdateLeaveRequestDepartmentNameByDepartmentIdAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                    .Index("leave_request")
                    .Query(q => q
                        .Term(t => t.Field(f => f.DepartmentId).Value(departmentId.ToString()))
                    )
                    .Script(s => s
                        .Source("ctx._source.departmentName = params.newDeptName; ctx._source.DepartmentName = params.newDeptName;")
                        .Params(p => p.Add("newDeptName", newDepartmentName))
                    )
                    .Refresh(true)
                    , ct), cancellationToken
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
                _logger.LogError(ex, "[ELASTIC C# UPDATE HATASI] Department bazlı izin güncellemesi başarısız.");
                throw;
            }
        }

        public async Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.DeleteAsync<EmployeeIndexModel>(id, d => d
                .Index("employees")
                , ct), cancellationToken
            );
        }

        public async Task<List<EmployeeDto>> SearchEmployeeAsync(string keyword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<EmployeeDto>();
            }

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
                        .Fuzziness(Fuzziness.Auto)
                    )
                )
                , ct), cancellationToken
            );

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Documents == null)
            {
                _logger.LogWarning($"[ELASTIC] SearchEmployee başarısız: {response.ServerError?.Error?.Reason}");
                return new List<EmployeeDto>();
            }

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
            var indexName = "leave_request";

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<LeaveIndexModel>();
            }

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index(indexName)
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(p => p.EmployeeName)
                            .Field(p => p.Description)
                        )
                        .Query(keyword)
                        .Fuzziness(Fuzziness.Auto)
                    )
                )
                , ct), cancellationToken
            );

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Documents == null)
            {
                _logger.LogWarning($"[ELASTIC] SearchLeaveRequest başarısız: {response.ServerError?.Error?.Reason}");
                return new List<LeaveIndexModel>();
            }

            return response.Documents.ToList();
        }

        public async Task UpdateLeaveRequestEmployeeNameAsync(Guid employeeId, string newEmployeeName, CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.UpdateByQueryAsync<LeaveIndexModel>(u => u
                .Index("leave_request")
                .Query(q => q
                    .Match(m => m.Field(f => f.EmployeeId).Query(employeeId.ToString()))
                )
                .Script(s => s
                    .Source("ctx._source.employeeName = params.newName;")
                    .Params(p => p.Add("newName", newEmployeeName))
                )
                .Refresh(true)
                , ct), cancellationToken
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

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.BulkAsync(b => b
                .Index("departments")
                .IndexMany(departments, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor)
                , ct), cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu Departman Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task BulkIndexEmployeesAsync(List<EmployeeIndexModel> employees, CancellationToken cancellationToken = default)
        {
            if (employees == null || !employees.Any()) return;

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.BulkAsync(b => b
                .Index("employees")
                .IndexMany(employees, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor)
                , ct), cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu Çalışan Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task BulkIndexLeaveRequestsAsync(List<LeaveIndexModel> leaveRequests, CancellationToken cancellationToken = default)
        {
            if (leaveRequests == null || !leaveRequests.Any()) return;

            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.BulkAsync(b => b
                .Index("leave_request")
                .IndexMany(leaveRequests, (descriptor, doc) => descriptor.Id(doc.Id.ToString()))
                .Refresh(Refresh.WaitFor)
                , ct), cancellationToken
            );

            if (response.Errors)
            {
                _logger.LogError($"Elasticsearch Toplu İzin Kayıt Hatası: {response.ServerError?.Error?.Reason}");
            }
        }

        public async Task<List<LeaveTypeStatDto>> GetLeaveTypeStatsFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
            .Index("leave_request")
            .Size(0)
            .Aggregations(a => a
            .Terms("tiplere_gore_grupla", t => t
            .Field(f => f.LeaveType)
            )
            )
            , ct), cancellationToken
            );

            // 🔥 GUARD CLAUSE: Patlamayı engeller
            if (!response.IsValid || response.Aggregations == null)
            {
                _logger.LogWarning($"[ELASTIC] GetLeaveTypeStatsFromElasticAsync başarısız oldu: {response.ServerError?.Error?.Reason}");
                return new List<LeaveTypeStatDto>();
            }

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
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Aggregations(a => a
                    .Terms("status_counts", t => t.Field(f => f.Status))
                    .Filter("total_approved_days", f => f
                        .Filter(fi => fi.Term(t => t.Field(field => field.Status).Value((int)LeaveRequestStatus.Approved)))
                        .Aggregations(childAggs => childAggs
                            .Sum("sum_days", sum => sum.Field(field => field.DurationDays))
                        )
                    )
                )
            , ct), cancellationToken);

            var result = new StatisticsOverviewDto();

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Aggregations == null)
            {
                _logger.LogWarning($"[ELASTIC] GetOverviewFromElasticAsync başarısız oldu: {response.ServerError?.Error?.Reason}");
                return result;
            }

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
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Aggregations(a => a
                    .Terms("depts", t => t
                        .Field(f => f.DepartmentName.Suffix("keyword"))
                        .Aggregations(childAggs => childAggs
                            .Sum("dept_sum_days", sum => sum.Field(f => f.DurationDays))
                        )
                    )
                )
            , ct), cancellationToken);

            var list = new List<DepartmentLeaveStatDto>();

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Aggregations == null)
            {
                _logger.LogWarning($"[ELASTIC] GetDepartmentStatsFromElasticAsync başarısız oldu: {response.ServerError?.Error?.Reason}");
                return list;
            }

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

        // 3. Aylık İzin Dağılımı (Grafik için)
        public async Task<List<MonthlyLeaveStatDto>> GetMonthlyLeavesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
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
            , ct), cancellationToken);

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Aggregations == null)
            {
                _logger.LogWarning($"[ELASTIC] GetMonthlyLeavesFromElasticAsync başarısız oldu: {response.ServerError?.Error?.Reason}");
                return new List<MonthlyLeaveStatDto>();
            }

            return response.Aggregations.DateHistogram("monthly_stats").Buckets.Select(b => new MonthlyLeaveStatDto
            {
                MonthName = b.Date.ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                TotalDays = (int)(b.Sum("days_sum").Value ?? 0)
            }).ToList();
        }

        // 4. En Eski 5 Bekleyen Talep
        public async Task<List<OldestPendingLeaveStatDto>> GetOldestPendingLeavesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Pending)))
                .Sort(sort => sort.Ascending(f => f.CreationTime))
                .Size(5)
            , ct), cancellationToken);

            // 🔥 GUARD CLAUSE
            if (!response.IsValid || response.Documents == null)
            {
                _logger.LogWarning($"[ELASTIC] GetOldestPendingLeavesFromElasticAsync başarısız oldu: {response.ServerError?.Error?.Reason}");
                return new List<OldestPendingLeaveStatDto>();
            }

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
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.DeleteAsync<LeaveIndexModel>(id, d => d.Index("leave_request"), ct), cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError($"[ELASTIC] İzin silinirken hata oluştu: {response.OriginalException.Message}");
            }
        }

        // 1. En Çok İzin Kullanan 5 Personel
        public async Task<List<TopEmployeeStatDto>> GetTopEmployeesFromElasticAsync(CancellationToken cancellationToken = default)
        {
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Approved)))
                .Aggregations(a => a
                    .Terms("per_employee", t => t
                        .Field(f => f.EmployeeName.Suffix("keyword"))
                        .Size(5)
                        .Aggregations(aa => aa
                            .Sum("total_days", sum => sum.Field(f => f.DurationDays))
                            .TopHits("employee_details", th => th
                                .Size(1)
                                .Source(src => src.Includes(i => i.Field(f => f.DepartmentName)))
                            )
                        )
                    )
                )
            , ct), cancellationToken);

            // Zaten zırhlı
            if (!response.IsValid || response.Aggregations == null) return new List<TopEmployeeStatDto>();

            return response.Aggregations.Terms("per_employee").Buckets.Select(b =>
            {
                var firstHit = b.TopHits("employee_details")?.Documents<LeaveIndexModel>().FirstOrDefault();
                string deptName = firstHit?.DepartmentName;

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
            var response = await _elasticPolicy.ExecuteAsync(async ct => await _elasticClient.SearchAsync<LeaveIndexModel>(s => s
                .Index("leave_request")
                .Size(0)
                .Query(q => q.Term(t => t.Field(f => f.Status).Value((int)LeaveRequestStatus.Rejected)))
                .Aggregations(a => a
                    .Terms("rejected_employees", t => t
                        .Field(f => f.EmployeeName.Suffix("keyword"))
                        .Size(5)
                        .Aggregations(aa => aa
                            .TopHits("employee_details", th => th
                                .Size(1)
                                .Source(src => src.Includes(i => i.Field(f => f.DepartmentName)))
                            )
                        )
                    )
                )
            , ct), cancellationToken);

            // Zaten zırhlı
            if (!response.IsValid || response.Aggregations == null) return new List<RejectedEmployeeStatDto>();

            return response.Aggregations.Terms("rejected_employees").Buckets.Select(b =>
            {
                var firstHit = b.TopHits("employee_details")?.Documents<LeaveIndexModel>().FirstOrDefault();
                string deptName = firstHit?.DepartmentName;

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