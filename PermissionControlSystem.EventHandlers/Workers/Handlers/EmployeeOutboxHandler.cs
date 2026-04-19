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
        private readonly IElasticSearchService _elastic;
        private readonly ILogger<EmployeeOutboxHandler> _logger;

        public string MessageType => "Employee";

        public EmployeeOutboxHandler(IElasticSearchService elastic, ILogger<EmployeeOutboxHandler> logger)
        {
            _elastic = elastic;
            _logger = logger;
        }

        public async Task ProcessAsync(string messageType, JsonElement data)
        {
            // 1. Önce değişkeni dışarıda tanımlıyoruz ki catch bloğu buna erişebilsin
            Guid employeeId = Guid.Empty;

            try
            {
                // 2. Değişkenin içini dolduruyoruz
                var action = data.TryGetProperty("Action", out var act) ? act.GetString() ?? "Unknown" : "Unknown";
                employeeId = data.TryGetProperty("EmployeeId", out var idElem)
                    ? idElem.GetGuid()
                    : (data.TryGetProperty("Id", out var fallbackIdElem) ? fallbackIdElem.GetGuid() : Guid.Empty);

                if (employeeId == Guid.Empty)
                {
                    return;
                }

                if (action == "Deleted" || messageType == "EmployeeDeleted")
                {
                    await _elastic.DeleteEmployeeAsync(employeeId);
                    _logger.LogInformation("[ELASTIC] Çalışan silindi: {EmployeeId}", employeeId);
                    return;
                }

                var departmentId = data.TryGetProperty("DepartmentId", out var departmentIdProp) && departmentIdProp.ValueKind != JsonValueKind.Null
                    ? departmentIdProp.GetGuid()
                    : Guid.Empty;
                var departmentNameRaw = data.TryGetProperty("DepartmentName", out var departmentNameProp) ? departmentNameProp.GetString() : null;
                var fullNameRaw = data.TryGetProperty("FullName", out var fullNameProp) ? fullNameProp.GetString() : null;
                var position = data.TryGetProperty("Position", out var positionProp) ? positionProp.GetString() ?? string.Empty : string.Empty;
                var email = data.TryGetProperty("Email", out var emailProp) ? emailProp.GetString() ?? string.Empty : string.Empty;

                var departmentName = string.IsNullOrWhiteSpace(departmentNameRaw) ? "Belirtilmemiş" : departmentNameRaw.Trim();
                var fullName = string.IsNullOrWhiteSpace(fullNameRaw) ? "Bilinmiyor" : fullNameRaw.Trim();

                await _elastic.IndexEmployeeAsync(employeeId, departmentId, departmentName, fullName, position, email);
                _logger.LogInformation("[ELASTIC] Çalışan indexlendi: {FullName}", fullName);

                if (action == "Updated" || messageType == "EmployeeUpdated")
                {
                    await _elastic.UpdateLeaveRequestEmployeeDetailsAsync(employeeId, fullName, departmentName);
                    _logger.LogInformation(
                        "[ELASTIC] Personelin leave_request kayıtları güncellendi. EmployeeId: {EmployeeId}",
                        employeeId);
                }
            }
            catch (Exception ex)
            {
                // Artık employeeId burada tanınıyor!
                _logger.LogError(ex, "❌ [OUTBOX FATAL ERROR] Personel verisi (ID: {EmployeeId}) Elastic'e işlenirken patladı!", employeeId);
                throw;
            }
        }
    }
}