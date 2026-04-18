using Microsoft.Extensions.Logging;
using PermissionControlSystem.Outbox.Interfaces;
using PermissionControlSystem.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace PermissionControlSystem.EventHandlers.Workers.Handlers
{
    public class DepartmentOutboxHandler : IOutboxHandler, ITransientDependency
    {
        private readonly IElasticSearchService _elastic;
        private readonly ILogger<DepartmentOutboxHandler> _logger;

        public string MessageType => "Department";

        public DepartmentOutboxHandler(IElasticSearchService elastic, ILogger<DepartmentOutboxHandler> logger)
        {
            _elastic = elastic;
            _logger = logger;
        }

        public async Task ProcessAsync(string type, JsonElement data)
        {
            // 🔥 MADDE 4: TÜM İŞLEMİ ZIRHLIYORUZ
            try
            {
                var action = data.TryGetProperty("Action", out var act) ? act.GetString() : "Unknown";
                var departmentId = data.TryGetProperty("DepartmentId", out var idElem)
                    ? idElem.GetGuid()
                    : (data.TryGetProperty("Id", out var fallbackIdElem) ? fallbackIdElem.GetGuid() : Guid.Empty);

                if (departmentId == Guid.Empty)
                {
                    return;
                }

                if (action == "Deleted" || type == "DepartmentDeleted")
                {
                    await _elastic.DeleteDepartmentAsync(departmentId);
                    _logger.LogInformation("[ELASTIC] Departman silindi: {DepartmentId}", departmentId);
                    return;
                }

                var name = data.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "Belirtilmemiş" : "Belirtilmemiş";
                var description = data.TryGetProperty("Description", out var descriptionProp) ? descriptionProp.GetString() ?? string.Empty : string.Empty;

                await _elastic.IndexDepartmentAsync(departmentId, name, description);
                _logger.LogInformation("[ELASTIC] Departman indexlendi: {DepartmentName}", name);

                if (action == "Updated" || type == "DepartmentUpdated")
                {
                    await _elastic.UpdateEmployeeDepartmentNameAsync(departmentId, name);
                    await _elastic.UpdateLeaveRequestDepartmentNameByDepartmentIdAsync(departmentId, name);

                    _logger.LogInformation(
                        "[ELASTIC] Departman güncellemesi tamamlandı. DepartmentId: {DepartmentId}, DepartmentName: {DepartmentName}",
                        departmentId,
                        name);
                }
            }
            catch (Exception ex)
            {
                // 1. Hatayı yakala ve spesifik olarak logla (Hangi veride patladık bilelim)
                _logger.LogError(ex, "❌ [OUTBOX FATAL ERROR] Department verisi Elastic'e işlenirken patladı!");

                // 2. 🔥 İŞTE MADDE 4'ÜN KAHRAMANI: THROW!
                // Bu sayede OutboxWorker "Aaa hata çıkmış, ben bunu Retry count artırıp sonra tekrar deneyeyim" diyecek!
                throw;
            }
        }
    }
}