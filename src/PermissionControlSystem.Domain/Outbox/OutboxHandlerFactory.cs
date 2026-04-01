using System.Collections.Generic;
using System.Linq;
using PermissionControlSystem.Outbox.Interfaces;
using Volo.Abp.Domain.Services;

namespace PermissionControlSystem.Outbox
{
    // DomainService'den miras alıyoruz ki ABP bu sınıfı otomatik olarak Dependency Injection'a (DI) kaydetsin.
    public class OutboxHandlerFactory : DomainService, IOutboxHandlerFactory
    {
        private readonly IEnumerable<IOutboxHandler> _handlers;

        // Sistemdeki tüm uzmanlar (Employee, Department vb.) buraya otomatik dökülür
        public OutboxHandlerFactory(IEnumerable<IOutboxHandler> handlers)
        {
            _handlers = handlers;
        }

        public IOutboxHandler GetHandler(string messageType)
        {
            // Eğer uzman listesi boşsa hata almamak için güvenlik önlemi
            if (_handlers == null || !_handlers.Any()) return null;

            // Gelen mesaj tipi (örn: "Employee") uzmanın alanıyla (örn: "Employee") eşleşiyor mu?
            return _handlers.FirstOrDefault(h => h.MessageType == messageType);
        }
    }
}