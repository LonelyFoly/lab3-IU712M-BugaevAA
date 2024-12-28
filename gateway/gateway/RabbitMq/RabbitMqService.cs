using RabbitMQ.Client;
using System;
using System.Text;
using System.Text.Json;
public interface IRabbitMqService
{
    void SendCancelPaymentMessage(string paymentUid);
}

public class RabbitMqService: IRabbitMqService
{


    public RabbitMqService()
    {
}

    public void SendCancelPaymentMessage(string paymentUid)
    {
        var message = (new { PaymentUid = paymentUid }).ToString();
        Console.WriteLine($"Sent to rabbit{message}");
        // var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var factory = new ConnectionFactory { Uri = new Uri("amqps://ksspkpds:FjJbWVFDglHNI_9l1IuFUuGr3ax2v8Sq@kebnekaise.lmq.cloudamqp.com/ksspkpds") };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: "MyQueue",
                           durable: false,
                           exclusive: false,
                           autoDelete: false,
                           arguments: null);

            var body = Encoding.UTF8.GetBytes(message);
            Console.WriteLine($"Sent to rabbit{body}");
            channel.BasicPublish(exchange: "",
                           routingKey: "MyQueue",
                           basicProperties: null,
                           body: body);

            //_channel.BasicPublish(exchange: "", routingKey: "cancel_payment_queue", basicProperties: null, body: body);
        }
    }
}