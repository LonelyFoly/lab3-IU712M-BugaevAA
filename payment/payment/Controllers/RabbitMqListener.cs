
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using payment.DB;
using System.Diagnostics;

namespace payment.Controllers
{
    
    public class RabbitMqListener : BackgroundService
    {
        dbHandler db = new dbHandler();
        private IConnection _connection;
        private IModel _channel;
        private ConnectionFactory factory;
        private EventingBasicConsumer? consumer;

        public RabbitMqListener()
        {
            Console.WriteLine($"Rabbit declared");
            factory = new ConnectionFactory { Uri = new Uri("amqps://ksspkpds:FjJbWVFDglHNI_9l1IuFUuGr3ax2v8Sq@kebnekaise.lmq.cloudamqp.com/ksspkpds") };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "MyQueue", durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            Console.WriteLine($"Subscription is declared");
            consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());

                // Каким-то образом обрабатываем полученное сообщение
                Console.WriteLine($"Recieved message {content}");
                var paymentMessage = JsonSerializer.Deserialize<dynamic>(content);
                string paymentUid = paymentMessage.PaymentUid;
                CancelPayment(paymentUid);
                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume("MyQueue", false, consumer);

            return Task.CompletedTask;
        }

        private void CancelPayment(string paymentUid)
        {
            Console.WriteLine($"Откат оплаты {paymentUid}");
            db.deletePayment(paymentUid);
        }
    }
}
