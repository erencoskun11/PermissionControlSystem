using Microsoft.Extensions.Logging;
using PermissionControlSystem.Outbox.Interfaces;
using PermissionControlSystem.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.EventHandlers.Workers.Handlers
{
    public class EmployeeOutboxHandler : IOutboxHandler, ITransientDependency
    {
        public string MessageType => "Employee";

        private readonly IElasticSearchService _elastic;
        private readonly ILogger<EmployeeOutboxHandler> _logger;

        public EmployeeOutboxHandler(IElasticSearchService elastic, ILogger<EmployeeOutboxHandler> logger)
        {
            _elastic = elastic;
            _logger = logger;
        }

        public async Task ProcessAsync(string messageType, JsonElement data)
        {
            // 🛡️ DEFANSİF: Action oku
            string action = data.TryGetProperty("Action", out var act) ? (act.GetString() ?? "Unknown") : "Unknown";

            // 🛡️ DEFANSİF: Id oku
            Guid employeeId = data.TryGetProperty("EmployeeId", out var idElem) ? idElem.GetGuid() : (data.TryGetProperty("Id", out var idElem2) ? idElem2.GetGuid() : Guid.Empty);

            if (employeeId == Guid.Empty) return; // Geçersiz veri, çık.

            if (action == "Deleted" || messageType == "EmployeeDeleted")
            {
                await _elastic.DeleteEmployeeAsync(employeeId); // Sadece Guid yolluyoruz
                _logger.LogInformation($"[ELASTIC] Çalışan silindi: {employeeId}");
                return;
            }

            // Created veya Updated işlemleri
            
                // 🔥 CS0037 ÇÖZÜMÜ: null yerine Guid.Empty kullanıyoruz
                var deptId = data.TryGetProperty("DepartmentId", out var dId) && dId.ValueKind != JsonValueKind.Null ? dId.GetGuid() : Guid.Empty;
                var deptNameRaw = data.TryGetProperty("DepartmentName", out var dName) ? dName.GetString() : null;
                var deptName = string.IsNullOrWhiteSpace(deptNameRaw) ? "Belirtilmemiş" : deptNameRaw.Trim();

                var fullNameRaw = data.TryGetProperty("FullName", out var fName) ? fName.GetString() : null;
                var fullName = string.IsNullOrWhiteSpace(fullNameRaw) ? "Bilinmiyor" : fullNameRaw.Trim();

                var position = data.TryGetProperty("Position", out var pos) ? pos.GetString() ?? "" : "";
                var email = data.TryGetProperty("Email", out var mail) ? mail.GetString() ?? "" : "";

                // 🔥 CS7036 ÇÖZÜMÜ: Model yerine parametreleri tek tek yolluyoruz
                await _elastic.IndexEmployeeAsync(employeeId, deptId, deptName, fullName, position, email);
                _logger.LogInformation($"[ELASTIC] Çalışan indexlendi: {fullName}");

                // 🔥 CASCADE UPDATE: çalışanın leave_request kayıtlarındaki denormalize alanları da güncelle
                if (action == "Updated" || messageType == "EmployeeUpdated")
                {
                    await _elastic.UpdateLeaveRequestEmployeeDetailsAsync(employeeId, fullName, deptName);
                    _logger.LogInformation($"[ELASTIC] Personelin leave_request kayıtları zincirleme güncellendi. EmployeeId: {employeeId}, Name: {fullName}, Department: {deptName}");
                }
            }
           
        
    }
}