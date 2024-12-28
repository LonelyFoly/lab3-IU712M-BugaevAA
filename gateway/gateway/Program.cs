
using gateway.RabbitMq;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpClient();
            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection("CircuitBreaker"));

            // Регистрация CircuitBreaker с использованием настроек
            builder.Services.AddSingleton<CircuitBreaker>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CircuitBreakerOptions>>().Value;
                return new CircuitBreaker(options.FailureThreshold, TimeSpan.FromSeconds(options.TimeoutSeconds));
            });

            builder.Services.AddSingleton<RabbitMqService>();  
            //builder.Services.AddSingleton<CancelPaymentHandler>();
            builder.Services.AddScoped<IRabbitMqService, RabbitMqService>();


            var app = builder.Build();

            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();
            /*app.MapReverseProxy();  // Прокси для всех маршрутов, определенных в appsettings.json
*/
            app.Run();
        }
    }
}
