using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;


namespace gateway.RabbitMq
{
    public class FailedRequestProcessor : BackgroundService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly CircuitBreaker _circuitBreaker;

        public FailedRequestProcessor(IHttpClientFactory clientFactory, CircuitBreaker circuitBreaker)
        {
            _clientFactory = clientFactory;
            _circuitBreaker = circuitBreaker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Bacground service is started =====================");
            var factory = new ConnectionFactory() { HostName = "rabbitmq", Port = 5672 };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Создаем очередь, если она не существует
            channel.QueueDeclare(queue: "failed-requests", durable: true, exclusive: false, autoDelete: false, arguments: null);

            // Создаем потребителя для очереди
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(body);

                if (message != null)
                {
                    var page = message["page"];
                    var size = message["size"];
                    Console.WriteLine("Bacground service is RECIEVED =====================");
                    try
                    {
                        var client = _clientFactory.CreateClient();
                        var response = await client.GetAsync($"http://reservation:8060/api/v1/hotels?page={page}&size={size}");

                        if (response.IsSuccessStatusCode)
                        {
                            _circuitBreaker.RegisterSuccess();
                            Console.WriteLine($"Request for page {page} and size {size} succeeded.");

                            // Успешно обработано, подтверждаем удаление сообщения из очереди
                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        else
                        {
                            _circuitBreaker.RegisterFailure();
                            Console.WriteLine($"Request for page {page} and size {size} failed. Re-queuing.");
                            EnqueueFailedRequest(page, size);  // Повторно ставим сообщение в очередь
                            channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true); // Сообщение возвращается в очередь
                        }
                    }
                    catch (Exception ex)
                    {
                        _circuitBreaker.RegisterFailure();
                        Console.WriteLine($"Error: {ex.Message}. Re-queuing.");
                        EnqueueFailedRequest(page, size);  // Повторно ставим сообщение в очередь
                        channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true); // Сообщение возвращается в очередь
                    }
                }
            };

            // Начинаем получать сообщения из очереди
            channel.BasicConsume(queue: "failed-requests", autoAck: false, consumer: consumer);
            while (!stoppingToken.IsCancellationRequested)
            {
                // Задержка перед следующим циклом, чтобы избежать излишней нагрузки на процессор
                await Task.Delay(1000, stoppingToken); // Периодически проверяем, не отменено ли выполнение
            }
            await Task.CompletedTask;
        }

        // Метод для постановки в очередь неудачных запросов
        private void EnqueueFailedRequest(int page, int size)
        {
            var factory = new ConnectionFactory() { HostName = "rabbitmq", Port = 5672 };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "failed-requests", durable: true, exclusive: false, autoDelete: false, arguments: null);

            var request = new { page, size };
            var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request);

            channel.BasicPublish(exchange: "", routingKey: "failed-requests", basicProperties: null, body: body);
        }
    }


}