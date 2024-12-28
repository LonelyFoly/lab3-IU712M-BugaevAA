using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using gateway.RabbitMq;
using RabbitMQ.Client;

namespace gateway.Controllers
{

    [ApiController]
    public class GatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _clientFactory;

        string username;

        int timer_seconds = 10;
        private static readonly CircuitBreaker _circuitBreakerRes
            = new CircuitBreaker(3, TimeSpan.FromSeconds(10));
        private static readonly CircuitBreaker _circuitBreakerLoy
            = new CircuitBreaker(3, TimeSpan.FromSeconds(10));
        private static readonly CircuitBreaker _circuitBreakerPay
            = new CircuitBreaker(3, TimeSpan.FromSeconds(10));

        private readonly Timer _healthCheckResTimer;
        private readonly Timer _healthCheckLoyTimer;
        private readonly Timer _healthCheckPayTimer;
        public GatewayController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            username = "Test Max";
            _healthCheckResTimer = new Timer(async _ => await CheckHealth_Reservation(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _healthCheckLoyTimer = new Timer(async _ => await CheckHealth_Loyalty(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _healthCheckPayTimer = new Timer(async _ => await CheckHealth_Payment(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

        }
        private async Task CheckHealth_Reservation()
        {
            var client = _clientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync("http://reservation:8060/manage/health");

                if (response.IsSuccessStatusCode)
                {
                    if (_circuitBreakerRes._isOpen)
                        Console.WriteLine($"Reservation is working after " +
                            $"{(DateTime.UtcNow - _circuitBreakerRes._lastFailureTime).TotalSeconds}");
                    _circuitBreakerRes.RegisterSuccess();
                }
            }
            catch (Exception)
            {

            }
        }
        private async Task CheckHealth_Loyalty()
        {
            var client = _clientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync("http://loyalty:8070/manage/health");

                if (response.IsSuccessStatusCode)
                {
                    if (_circuitBreakerRes._isOpen)
                        Console.WriteLine($"Loyalty is working after " +
                            $"{(DateTime.UtcNow - _circuitBreakerRes._lastFailureTime).TotalSeconds}");
                    _circuitBreakerRes.RegisterSuccess();
                }
            }
            catch (Exception)
            {

            }
        }
        private async Task CheckHealth_Payment()
        {
            var client = _clientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync("http://payment:8050/manage/health");

                if (response.IsSuccessStatusCode)
                {
                    if (_circuitBreakerRes._isOpen)
                        Console.WriteLine($"Payment is working after " +
                            $"{(DateTime.UtcNow - _circuitBreakerRes._lastFailureTime).TotalSeconds}");
                    _circuitBreakerRes.RegisterSuccess();
                }
            }
            catch (Exception)
            {

            }
        }

        [HttpGet("/manage/health")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }
        //rabbitMQ
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

        [HttpGet("/api/v1/hotels")]
        public async Task<IActionResult> ProxyReservationService([FromQuery] int page, [FromQuery] int size)
        {
            if (_circuitBreakerRes.IsOpen())
            {
                // fallback: если цепочка открыта (неудачные попытки), возвращаем пустой ответ
                return Ok(new
                {
                    items = new List<object>(),
                    page,
                    pageSize = size,
                    totalElements = 0
                });
            }

            try
            {
                var client = _clientFactory.CreateClient();
                var response = await client.GetAsync($"http://reservation:8060/api/v1/hotels?page={page}&size={size}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadFromJsonAsync<hotel[]>();
                    _circuitBreakerRes.RegisterSuccess();

                    var result = new
                    {
                        items = content.Select(item => new
                        {
                            item.id,
                            item.name,
                            item.city,
                            item.country,
                            item.address,
                            item.hotelUid,
                            item.price,
                            item.stars
                        }).ToList(),
                        page,
                        pageSize = size,
                        totalElements = content.Length
                    };
                    return Ok(result);
                }

                _circuitBreakerRes.RegisterFailure();
                EnqueueFailedRequest(page, size);
                return StatusCode((int)response.StatusCode);
            }
            catch (Exception)
            {
                _circuitBreakerRes.RegisterFailure();
                EnqueueFailedRequest(page, size);

                // fallback: если ошибка, возвращаем пустой ответ
                return Ok(new
                {
                    items = new List<object>(),
                    page,
                    pageSize = size,
                    totalElements = 0
                });
            }
        }

        [HttpGet("/api/v1/reservations")]
        public async Task<IActionResult> ReservateMe()
        {
            if (_circuitBreakerRes.IsOpen())
            {
                // fallback если CircuitBreaker открыт
                return Ok(new
                {
                    reservations = new List<object>()
                });
            }

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response = await client.GetAsync($"http://reservation:8060/api/v1/reservations");

            if (!response.IsSuccessStatusCode)
            {
                _circuitBreakerRes.RegisterFailure();
                EnqueueFailedRequest("reservations", username); // Добавляем запрос в очередь на повтор
                return StatusCode((int)response.StatusCode);
            }

            var content = await response.Content.ReadFromJsonAsync<reservation[]>();

            var _hotels = new Dictionary<string, hotel>();
            var _payments = new Dictionary<string, payment>();

            foreach (var item in content)
            {
                var hotel = await GetHotelInfo(client, item.hotelUid);
                if (hotel == null)
                {
                    _circuitBreakerRes.RegisterFailure();
                    EnqueueFailedRequest("hotel", item.hotelUid.ToString()); 
                    return NotFound();
                }

                var payment = await GetPaymentInfo(client, item.paymentUid);
                if (payment == null)
                {
                    _circuitBreakerRes.RegisterFailure();
                    EnqueueFailedRequest("payment", item.paymentUid.ToString()); 
                    return NotFound();
                }

                _hotels[item.hotelUid.ToString()] = hotel;
                _payments[item.paymentUid.ToString()] = payment;
            }

            var result = content.Select(item => new
            {
                reservationUid = item.reservationUid,
                hotel = new
                {
                    hotelUid = _hotels[item.hotelUid.ToString()].hotelUid,
                    name = _hotels[item.hotelUid.ToString()].name,
                    fullAddress = $"{_hotels[item.hotelUid.ToString()].country}, {_hotels[item.hotelUid.ToString()].city}, {_hotels[item.hotelUid.ToString()].address}",
                    stars = _hotels[item.hotelUid.ToString()].stars,
                },
                startDate = item.startDate.ToString("yyyy-MM-dd"),
                endDate = item.endDate.ToString("yyyy-MM-dd"),
                status = item.status,
                payment = new
                {
                    status = _payments[item.paymentUid.ToString()].status,
                    price = _payments[item.paymentUid.ToString()].price
                }
            }).ToList();

            _circuitBreakerRes.RegisterSuccess(); // Успешная обработка

            return Ok(result);
        }


        private async Task<hotel> GetHotelInfo(HttpClient client, Guid hotelUid)
        {
            var response = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{hotelUid}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<hotel>(content);
            }

            return null;
        }


        private async Task<payment> GetPaymentInfo(HttpClient client, Guid paymentUid)
        {
            var response = await client.GetAsync($"http://payment:8050/api/v1/payment/{paymentUid}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<payment>(content);
            }

            return null; 
        }

        // Метод для постановки в очередь неудачных запросов
        private void EnqueueFailedRequest(string type, string identifier)
        {
            var factory = new ConnectionFactory() { HostName = "rabbitmq", Port = 5672 };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "failed-requests", durable: true, exclusive: false, autoDelete: false, arguments: null);

            var request = new { type, identifier };
            var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request);

            channel.BasicPublish(exchange: "", routingKey: "failed-requests", basicProperties: null, body: body);
        }
        [HttpGet("/api/v1/reservations/{reservationUid}")]
        public async Task<IActionResult> CheckReservateMe(Guid reservationUid)
        {

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response = await client.GetAsync($"http://reservation:8060/api/v1/reservation/{reservationUid}");
            var content = await response.Content.ReadFromJsonAsync<reservation>();

            var response2 = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{content.hotelUid}");
            var content2 = await response2.Content.ReadAsStringAsync();
            if (!response2.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 2");
                Console.WriteLine($"{content.hotelUid}");
                return NotFound();
            }
            hotel _hotel = JsonSerializer.Deserialize<hotel>(content2);



            var response3 = await client.GetAsync($"http://payment:8050/api/v1/payment/{content.paymentUid}");

            if (!response3.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 3");
                return NotFound();
            }
            var content3 = await response3.Content.ReadAsStringAsync();
            payment payment = JsonSerializer.Deserialize<payment>(content3);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new
                {
                    hotel = new {
                        hotelUid = content.hotelUid,
                        name = _hotel.name,
                        fullAddress = _hotel.country +", "+
                        _hotel.city+", " +
                        _hotel.address,
                        stars = _hotel.stars
                    },
                    reservationUid = content.reservationUid,
                    startDate = content.startDate.ToString("yyyy-MM-dd"),
                    endDate = content.endDate.ToString("yyyy-MM-dd"),
                    status = content.status,
                    payment = new
                    {
                        status = payment.status,
                        price = payment.price
                    }

                });
            }

            return NotFound();
        }
        [HttpGet("api/v1/loyalty")]
        public async Task<IActionResult> GetLoyalty()
        {

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response = await client.GetAsync(
                $"http://loyalty:8070/api/v1/loyalty");
            var content = await response.Content.ReadFromJsonAsync<loyalty>();


            if (response.IsSuccessStatusCode)
            {
                var result = new
                {
                    status = content.status,
                    discount = content.discount,
                    reservationCount = content.reservationCount,
                };
                return Ok(result);

            }

            return StatusCode((int)response.StatusCode, content);
        }

        [HttpPost("/api/v1/reservations")]
        public async Task<IActionResult> PostReservation([FromBody] DateForm df)
        {
            TimeSpan difference = df.endDate- df.startDate;

            // Получаем количество дней
            double totalDays = difference.TotalDays;

                
            var client = _clientFactory.CreateClient();
            
            var response = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{df.hotelUid}");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 1");
                return NotFound();
            }
            hotel _hotel = JsonSerializer.Deserialize<hotel>(content);
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response2 = await client.GetAsync($"http://loyalty:8070/api/v1/loyalty");
            var content2 = await response2.Content.ReadAsStringAsync();


            if (!response2.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 2");
                return NotFound();
            }
                    
            loyalty _loyalty = JsonSerializer.Deserialize<loyalty>(content2);
            double q = _loyalty.discount;
            Console.WriteLine($"q = {q}, _hotel.price= {_hotel.price}," +
                $"totalDays = {totalDays}");
            int price_sum = Convert.ToInt32 (
                Math.Round((100 - q)*0.01 * _hotel.price * totalDays)
                );
            Console.WriteLine($"price_sum = {price_sum}");
            Guid paymentUid = Guid.NewGuid();
            var json = JsonSerializer.Serialize(new PaymentToDo(paymentUid, price_sum));

            //передаёт информацию о новой оплате
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response3 = await client.PostAsync($"http://payment:8050/api/v1/payment", httpContent);
            var content3 = await response3.Content.ReadAsStringAsync();
            if (!response3.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 3");
                return NotFound(content3);
            }
            var response4 = await client.PatchAsync(
                $"http://loyalty:8070/api/v1/loyaltyInc", null
                );
            var content4 = await response4.Content.ReadAsStringAsync();
            if (!response4.IsSuccessStatusCode)
            {
                Console.WriteLine("====response 4");
                return NotFound();
            }

            //add reservation
            Guid reservationUid = Guid.NewGuid();
            json = JsonSerializer.Serialize(new reservation(
                reservationUid,
                username,
                paymentUid,
                _hotel.hotelUid,
                "PAID",
                df.startDate,
                df.endDate
                ));

            var sendContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response5 = await client.PostAsync($"http://reservation:8060/api/v1/reservation", sendContent);
            var content5 = await response5.Content.ReadAsStringAsync();

            return Ok( new
            {
                reservationUid= reservationUid,
                hotelUid = _hotel.hotelUid,
                startDate = df.startDate.ToString("yyyy-MM-dd"),
                endDate = df.endDate.ToString("yyyy-MM-dd"),
                discount = _loyalty.discount,
                status = "PAID",
                payment = new
                {
                    status = "PAID",
                    price = price_sum,
                }
            }
                
                );
            
        }
        [HttpDelete("api/v1/reservations/{reservationUid}")]
        public async Task<IActionResult> CancelReservation(Guid reservationUid)
        {
            //обращение к reservation -> статус по uuid: CANCELED
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response = await client.PatchAsync(
                $"http://reservation:8060/api/v1/reservation/{reservationUid}", null
                );
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
               // Console.WriteLine("!Reservation====response 1");

                return NotFound(content);
            }

            reservation _res = JsonSerializer.Deserialize<reservation>(content);

            var response2 = await client.PatchAsync(
                $"http://payment:8050/api/v1/payment/{_res.paymentUid}", null
                );
            var content2 = await response2.Content.ReadAsStringAsync();
            if (!response2.IsSuccessStatusCode)
            {
                //Console.WriteLine("Reservation====response 2");
                return NotFound(content2);
            }


            var response3 = await client.PatchAsync(
                "http://loyalty:8070/api/v1/loyaltyDecrease", null
                );
            var content3 = await response3.Content.ReadAsStringAsync();
            if (!response3.IsSuccessStatusCode)
            {
                //Console.WriteLine("!!!!!Reservation====response 2");
                return NotFound(content3);
            }

            return NoContent();
        }

        [HttpGet("api/v1/me")]
        public async Task<IActionResult> ReservationMe()
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            var response = await client.GetAsync($"http://reservation:8060/api/v1/reservations");
            var content = await response.Content.ReadFromJsonAsync<reservation[]>();

            var _hotels = new Dictionary<string, hotel>();
            var _payments = new Dictionary<string, payment>();

            foreach (var item in content)
            {
                if (!_hotels.ContainsKey(item.hotelUid.ToString()))
                {
                    var response1 = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{item.hotelUid}");
                    if (response1.IsSuccessStatusCode)
                    {
                        var content1 = await response1.Content.ReadAsStringAsync();
                        _hotels[item.hotelUid.ToString()] = JsonSerializer.Deserialize<hotel>(content1);
                    }
                    else
                    {
                        Console.WriteLine($"====response 1 failed for hotelUid: {item.hotelUid}");
                        return NotFound();
                    }
                }
                if (!_payments.ContainsKey(item.paymentUid.ToString()))
                {
                    var response3 = await client.GetAsync($"http://payment:8050/api/v1/payment/{item.paymentUid}");
                    Console.WriteLine($"    paymentUUid: {item.paymentUid}");
                    if (!response3.IsSuccessStatusCode)
                    {
                        Console.WriteLine("====response 2");
                        return NotFound();
                    }
                    var content3 = await response3.Content.ReadAsStringAsync();
                    _payments[item.paymentUid.ToString()] = JsonSerializer.Deserialize<payment>(content3);

                }

            }

            var response4 = await client.GetAsync(
                $"http://loyalty:8070/api/v1/loyalty");
            var content4 = await response4.Content.ReadFromJsonAsync<loyalty>();




                if (response.IsSuccessStatusCode)
            {
                var result = new
                {
                    reservations = content.Select(item => new
                    {
                        reservationUid = item.reservationUid,
                        hotel = new
                        {
                            hotelUid = _hotels[item.hotelUid.ToString()].hotelUid,
                            name = _hotels[item.hotelUid.ToString()].name,
                            fullAddress = _hotels[item.hotelUid.ToString()].country + ", " + _hotels[item.hotelUid.ToString()].city + ", " + _hotels[item.hotelUid.ToString()].address,
                            stars = _hotels[item.hotelUid.ToString()].stars,
                        },

                        startDate = item.startDate.ToString("yyyy-MM-dd"),
                        endDate = item.endDate.ToString("yyyy-MM-dd"),
                        status = item.status,
                        payment = new
                        {
                            status = _payments[item.paymentUid.ToString()].status,
                            price = _payments[item.paymentUid.ToString()].price
                        }
                    }).ToList(),
                    loyalty = new
                    {
                        status = content4.status,
                        discount = content4.discount
                    }
                };
                return Ok(result)
                    ;
            }

            return StatusCode((int)response.StatusCode, content);
        }

    }
}