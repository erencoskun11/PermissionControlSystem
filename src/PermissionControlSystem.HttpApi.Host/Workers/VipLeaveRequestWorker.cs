using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PermissionControlSystem.Events;
using Volo.Abp.Emailing;
using Volo.Abp;

namespace PermissionControlSystem.Workers 
{
    public class VipLeaveRequestWorker : BackgroundService
    {
        private readonly ILogger<VipLeaveRequestWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private IConnection? _connection;
        private IChannel? _channel; // v7'deki en büyük değişiklik: IModel gitti, IChannel geldi!

        public VipLeaveRequestWorker(
            ILogger<VipLeaveRequestWorker> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        private async Task InitRabbitMqAsync()
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Connections:Default:HostName"] ?? throw new InvalidOperationException("RabbitMQ:Connections:Default:HostName configuration is required."),
                Port = int.Parse(_configuration["RabbitMQ:Connections:Default:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Connections:Default:UserName"] ?? throw new InvalidOperationException("RabbitMQ:Connections:Default:UserName configuration is required."),
                Password = _configuration["RabbitMQ:Connections:Default:Password"] ?? throw new InvalidOperationException("RabbitMQ:Connections:Default:Password configuration is required."),
                VirtualHost = _configuration["RabbitMQ:Connections:Default:VirtualHost"] ?? throw new InvalidOperationException("RabbitMQ:Connections:Default:VirtualHost configuration is required.")
            };

            // v7: Bağlantı ve kanal oluşturma artık asenkron!
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            string queueName = "PermissionSystem_VIP_Queue"; // Yeni fiziksel kuyruğumuz

            // 🚀 YENİ: Kendi özel exchange'imiz!
            string exchangeName = "VIP_Emergency_Exchange";

            string routingKey = "leave.request.created.urgent"; // VIP etiketimiz

            // 🔥 BURASI KRİTİK: Yeni exchange'i "Topic" tipinde fiziksel olarak oluşturuyoruz
            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true);


            // Kuyruğu ve Bağlantıyı oluştur (Async)
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);
            _logger.LogInformation($"✅ [VIP WORKER] '{exchangeName}' (Topic) üzerinden VIP sistemi ayağa kalktı.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Worker çalışmaya başladığında önce RabbitMQ'yu hazırla
            await InitRabbitMqAsync();

            // v7: Artık tüketici de asenkron olmak ZORUNDA
            var channel = _channel ?? throw new InvalidOperationException("VIP RabbitMQ channel is not initialized.");
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);

                    var eventData = JsonSerializer.Deserialize<UrgentLeaveRequestCreatedEto>(messageJson)
                        ?? throw new InvalidOperationException("VIP queue message could not be deserialized.");

                    _logger.LogWarning($"🚨 [VIP KANAL - FİZİKSEL KUYRUK] {eventData.EmployeeName} için Hastalık İzni maili atılıyor!");

                    // IEmailSender'ı Scope içinden çağırıyoruz
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                        var managerEmail = _configuration["Settings:Email:ManagerEmail"];
                        Check.NotNullOrWhiteSpace(managerEmail, "Settings:Email:ManagerEmail");
                        var emailBody = $"<h3>🚨 SAĞLIK ACİLİYETİ</h3><p>{eventData.EmployeeName} - {eventData.Reason}</p>";

                        await emailSender.SendAsync(managerEmail, "🚨 ACİL: Hastalık İzni", emailBody, true);
                    }

                    // v7: Onay (Ack) verme işlemi asenkron oldu
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                    _logger.LogInformation("✅ [VIP KANAL] Mail başarıyla iletildi ve mesaj kuyruktan silindi.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "VIP Mesaj işlenirken hata oluştu!");
                    // v7: Hata durumunda Red (Nack) verme işlemi asenkron oldu
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            // Tüketiciyi kuyruğa bağla ve dinlemeye başla (Async)
            await channel.BasicConsumeAsync(queue: "PermissionSystem_VIP_Queue", autoAck: false, consumer: consumer);

            // Arka plan işçisinin kapanmaması için sonsuz döngüde beklet
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Uygulama kapanırken bağlantıları temiz bir şekilde kapat (Async)
            if (_channel != null) await _channel.CloseAsync();
            if (_connection != null) await _connection.CloseAsync();

            await base.StopAsync(cancellationToken);
        }
    }
}