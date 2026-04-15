using Microsoft.Extensions.Logging;
using PermissionControlSystem.Models;
using PermissionControlSystem.Outbox.Interfaces;
using PermissionControlSystem.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.EventHandlers.Workers.Handlers
{
    public class LeaveRequestOutboxHandler : IOutboxHandler, ITransientDependency
    {
        private readonly IElasticSearchService _elastic;
        private readonly ILogger<LeaveRequestOutboxHandler> _logger;

        public string MessageType => "LeaveRequest";

        public LeaveRequestOutboxHandler(IElasticSearchService elastic, ILogger<LeaveRequestOutboxHandler> logger)
        {
            _elastic = elastic;
            _logger = logger;
        }

        public async Task ProcessAsync(string type, JsonElement data)
        {
            var action = data.TryGetProperty("Action", out var act) ? act.GetString() : "Unknown";
            var leaveId = data.TryGetProperty("Id", out var idProp) ? idProp.GetGuid() : Guid.Empty;

            if (leaveId == Guid.Empty) return;

            // 1. SİLME İŞLEMİ
            if (action == "Deleted" || type == "LeaveRequestDeleted")
            {
                await _elastic.DeleteLeaveRequestAsync(leaveId);
                _logger.LogInformation($"[ELASTIC] İzin silindi: {leaveId}");
                return;
            }

            // 2. KAYIT VE GÜNCELLEME İŞLEMİ
           
                var model = new LeaveIndexModel
                {
                    Id = leaveId,
                    EmployeeId = data.TryGetProperty("EmployeeId", out var empId) ? empId.GetGuid() : Guid.Empty,
                    EmployeeName = data.TryGetProperty("EmployeeName", out var eName) ? eName.GetString() ?? "Bilinmiyor" : "Bilinmiyor",
                    DepartmentId = data.TryGetProperty("DepartmentId", out var dId) && dId.ValueKind != JsonValueKind.Null ? dId.GetGuid() : Guid.Empty,
                    DepartmentName = data.TryGetProperty("DepartmentName", out var dName) ? dName.GetString() ?? "Bilinmiyor" : "Bilinmiyor",
                    Description = data.TryGetProperty("Reason", out var reason) ? reason.GetString() ?? "" : "",
                    Status = data.TryGetProperty("Status", out var status) ? status.GetInt32() : 0,
                    LeaveType = data.TryGetProperty("LeaveType", out var lType) ? lType.GetInt32() : 0,
                    DurationDays = data.TryGetProperty("DurationDays", out var days) ? days.GetInt32() : 0,
                    StartDate = data.TryGetProperty("StartDate", out var start) ? start.GetDateTime() : DateTime.Now,
                    EndDate = data.TryGetProperty("EndDate", out var end) ? end.GetDateTime() : DateTime.Now,
                    CreationTime = data.TryGetProperty("CreationTime", out var create) ? create.GetDateTime() : DateTime.Now
                };

                await _elastic.IndexLeaveRequestAsync(model);
                _logger.LogInformation($"[ELASTIC] İzin başarıyla indekslendi. ID: {model.Id}");
            }
            
        
    }
}