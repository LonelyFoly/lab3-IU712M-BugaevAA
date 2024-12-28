using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using loyalty.DB;

namespace loyalty.Controllers
{
    public class LoyaltyQueueListener : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IHttpClientFactory _clientFactory;
        private ConnectionFactory factory;
        private EventingBasicConsumer? consumer;

        public LoyaltyQueueListener(IHttpClientFactory clientFactory)
        {
            this._clientFactory = clientFactory;
            Console.WriteLine($"Rabbit declared");
            factory = new ConnectionFactory { HostName = "kebnekaise", Port = 5672 };
           _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "LoyaltyQueue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"Recieved message {content}");
                try
                {
                    string username ="";
                    var paymentMessage = JsonSerializer.Deserialize<PaymentMessage>(content);
                    if (paymentMessage == null)
                    {
                        Console.WriteLine("Ошибка десериализации сообщения1");
                        return;
                        
                    }
                    username = paymentMessage.username;
                    Console.WriteLine($"Username: {username}");


                    var client = _clientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("X-User-Name", username);
                    var response = await client.PatchAsync("http://loyalty:8070/api/v1/loyaltyDecrease", null);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Успешно обработано сообщение для пользователя {username}");
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    else
                    {
                        throw new Exception("Loyalty Service не доступен.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
                    await Task.Delay(10000); 
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: "LoyaltyQueue", autoAck: false, consumer);
            return Task.CompletedTask;
        }
    }

}
