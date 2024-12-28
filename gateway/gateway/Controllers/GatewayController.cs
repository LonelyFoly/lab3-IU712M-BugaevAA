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
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Drawing;
using System.Net.NetworkInformation;

namespace gateway.Controllers
{

    [ApiController]
    public class GatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _clientFactory;

        string username;

        private readonly RabbitMqService _rabbitMqService;
        private static readonly CircuitBreaker _circuitBreakerRes
            = new CircuitBreaker(3, TimeSpan.FromSeconds(75));
        private static readonly CircuitBreaker _circuitBreakerLoy
            = new CircuitBreaker(3, TimeSpan.FromSeconds(75));
        private static readonly CircuitBreaker _circuitBreakerPay
            = new CircuitBreaker(3, TimeSpan.FromSeconds(75));

        public GatewayController(IHttpClientFactory clientFactory, RabbitMqService rabbitMqService)
        {
            _clientFactory = clientFactory;
            username = "Test Max";
            _circuitBreakerRes.timer = new Timer(async _ => await CheckHealth_Reservation(), null, TimeSpan.Zero, TimeSpan.FromDays(2));
            _circuitBreakerLoy.timer = new Timer(async _ => await CheckHealth_Loyalty(), null, TimeSpan.Zero, TimeSpan.FromDays(2));
            _circuitBreakerPay.timer = new Timer(async _ => await CheckHealth_Payment(), null, TimeSpan.Zero, TimeSpan.FromDays(2));
            _rabbitMqService = rabbitMqService;
        }
        private async Task CheckHealth_Reservation()
        {
            var client = _clientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync("http://reservation:8060/manage/health");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Reservation is working after " +
                        $"{(DateTime.UtcNow - _circuitBreakerRes._lastFailureTime).TotalSeconds}");
                    _circuitBreakerRes.RegisterSuccess();

                }
            }
            catch (Exception)
            {
                _circuitBreakerRes.RegisterFailure();
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
                        Console.WriteLine($"Loyalty is working after " +
                            $"{(DateTime.UtcNow - _circuitBreakerLoy._lastFailureTime).TotalSeconds}");
                        _circuitBreakerLoy.RegisterSuccess();
                }
            }
            catch (Exception)
            {
                _circuitBreakerLoy.RegisterFailure();
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
                    Console.WriteLine($"Payment is working after " +
                        $"{(DateTime.UtcNow - _circuitBreakerPay._lastFailureTime).TotalSeconds}");
                    _circuitBreakerPay.RegisterSuccess();

                }
            }
            catch (Exception)
            {
                _circuitBreakerPay.RegisterFailure();
            }
        }

        [HttpGet("/manage/health")]
        public IActionResult CheckHealth()
        {
            return Ok();
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
                else 
                    return StatusCode(500);

            }
            catch (Exception)
            {
                _circuitBreakerRes.RegisterFailure();
                return StatusCode(500);
            }
        }

        [HttpGet("/api/v1/reservations")]
        public async Task<IActionResult> ReservateMe()
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            System.Net.Http.HttpResponseMessage response;
            try 
            { 
                 response = await client.GetAsync($"http://reservation:8060/api/v1/reservations");
            }
            catch (Exception ex)
            {
                _circuitBreakerRes.RegisterFailure();
                return StatusCode(500);
            }


            if (!response.IsSuccessStatusCode)
            {
                _circuitBreakerRes.RegisterFailure();
                return StatusCode(500);
            }
            else
                _circuitBreakerRes.RegisterSuccess();

            var content = await response.Content.ReadFromJsonAsync<reservation[]>();


            var _hotels = new Dictionary<string, hotel>();
            var _payments = new Dictionary<string, payment>();

            foreach (var item in content)
            {
                if (!_hotels.ContainsKey(item.hotelUid.ToString()) && !_circuitBreakerRes.IsOpen())
                {
                    System.Net.Http.HttpResponseMessage response1;
                    try
                    {
                        response1 = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{item.hotelUid}");
                        if (response1.IsSuccessStatusCode)
                        {
                            _circuitBreakerRes.RegisterSuccess();
                            var content1 = await response1.Content.ReadAsStringAsync();
                            _hotels[item.hotelUid.ToString()] = JsonSerializer.Deserialize<hotel>(content1);
                            Console.WriteLine($"    hotelUid: {item.hotelUid}");
                        }
                        else
                        {
                            _circuitBreakerRes.RegisterFailure();
                        }
                    }
                    catch {
                        _circuitBreakerRes.RegisterFailure();
                    }
                    
                   
                }
                if (!_payments.ContainsKey(item.paymentUid.ToString()) && !_circuitBreakerPay.IsOpen())
                {
                    System.Net.Http.HttpResponseMessage response3;
                    try
                    {
                        response3 = await client.GetAsync($"http://payment:8050/api/v1/payment/{item.paymentUid}");
                        Console.WriteLine($"    paymentUUid: {item.paymentUid}");
                        if (!response3.IsSuccessStatusCode)
                        {

                            _circuitBreakerPay.RegisterFailure();
                        }
                        else
                        {
                            _circuitBreakerPay.RegisterSuccess();
                            var content3 = await response3.Content.ReadAsStringAsync();
                            _payments[item.paymentUid.ToString()] = JsonSerializer.Deserialize<payment>(content3);
                        }
                    }
                    catch
                    {
                        _circuitBreakerPay.RegisterFailure();
                        continue;
                    }
                    

                }
            }
            //Console.WriteLine($"Quantity of HOTELS: {_hotels.Count}");
            var result = content.Select(item => new
            {
                reservationUid = item.reservationUid,
                hotel = _hotels.Count == 0 ? null : new
                {
                    hotelUid = _hotels[item.hotelUid.ToString()].hotelUid,
                    name = _hotels[item.hotelUid.ToString()].name,
                    fullAddress = _hotels[item.hotelUid.ToString()].country + ", " + _hotels[item.hotelUid.ToString()].city + ", " + _hotels[item.hotelUid.ToString()].address,
                    stars = _hotels[item.hotelUid.ToString()].stars,
                },

                startDate = item.startDate.ToString("yyyy-MM-dd"),
                endDate = item.endDate.ToString("yyyy-MM-dd"),
                status = item.status,
                payment = _payments.Count == 0 ? null : new
                {
                    status = _payments[item.paymentUid.ToString()].status,
                    price = _payments[item.paymentUid.ToString()].price
                }
            }).ToList();
            return Ok(result); 
            
            
        }

        [HttpGet("/api/v1/reservations/{reservationUid}")]
        public async Task<IActionResult> CheckReservateMe(Guid reservationUid)
        {

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            System.Net.Http.HttpResponseMessage response;
            reservation? content;
            try
            {
                response = await client.GetAsync($"http://reservation:8060/api/v1/reservation/{reservationUid}");
                content = await response.Content.ReadFromJsonAsync<reservation>();
                if (!response.IsSuccessStatusCode)
                {
                    _circuitBreakerRes.RegisterFailure();
                    return StatusCode(500);
                }
                else
                    _circuitBreakerRes.RegisterSuccess();
            }
            catch
            {
                _circuitBreakerRes.RegisterFailure();
                return StatusCode(500);

            }
            System.Net.Http.HttpResponseMessage response2;
            string? content2;
            hotel? _hotel;
            try
            {
                response2 = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{content.hotelUid}");
                content2 = await response2.Content.ReadAsStringAsync();
                if (!response2.IsSuccessStatusCode)
                {
                    _hotel = null;
                    _circuitBreakerRes.RegisterFailure();
                }
                else
                {
                    _circuitBreakerRes.RegisterSuccess();
                    _hotel = JsonSerializer.Deserialize<hotel>(content2);
                }
            }
            catch
            {
                _circuitBreakerRes.RegisterFailure();
                _hotel = null;
            }
            payment? payment;
            try
            {
                var response3 = await client.GetAsync($"http://payment:8050/api/v1/payment/{content.paymentUid}");
                var content3 = await response3.Content.ReadAsStringAsync();
                if (!response3.IsSuccessStatusCode)
                {
                    payment = null;
                    _circuitBreakerPay.RegisterFailure();
                }
                else {
                    _circuitBreakerPay.RegisterSuccess();
                    payment = JsonSerializer.Deserialize<payment>(content3);
                }
            }
            catch {
                payment = null;
                
            }
            return Ok(new
            {
                hotel = _hotel == null ? null : new
                {
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
                payment = payment == null ? null : new
                {
                    status = payment.status,
                    price = payment.price
                }

            });
        }
        [HttpGet("api/v1/loyalty")]
        public async Task<IActionResult> GetLoyalty()
        {
            
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            try
            {
                var response = await client.GetAsync(
                $"http://loyalty:8070/api/v1/loyalty");
                var content = await response.Content.ReadFromJsonAsync<loyalty>();


                if (response.IsSuccessStatusCode)
                {
                    _circuitBreakerLoy.RegisterSuccess();
                    var result = new
                    {
                        status = content.status,
                        discount = content.discount,
                        reservationCount = content.reservationCount,
                    };
                    return Ok(result);

                }
                else
                {
                    _circuitBreakerLoy.RegisterFailure();
                    return StatusCode(503);
                }
            }
            catch (Exception ex)
            {
                _circuitBreakerLoy.RegisterFailure();
                
            }
            return StatusCode(503);
        }

        [HttpPost("/api/v1/reservations")]
        public async Task<IActionResult> PostReservation([FromBody] DateForm df)
        {
            TimeSpan difference = df.endDate- df.startDate;
            double totalDays = difference.TotalDays;
            var client = _clientFactory.CreateClient();
            string? content;
            try
            {
                var response = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{df.hotelUid}");
                content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("====response 1");
                    return NotFound();
                }
            } catch (Exception ex)
            {

                return StatusCode(503);
            }
               
            hotel _hotel = JsonSerializer.Deserialize<hotel>(content);
            client.DefaultRequestHeaders.Add("X-User-Name", username);

            double q = 10;//_loyalty.discount;
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


            try
            {
                var response4 = await client.PatchAsync(
                    $"http://loyalty:8070/api/v1/loyaltyInc", null
                    );
                var content4 = await response4.Content.ReadAsStringAsync();
                if (!response4.IsSuccessStatusCode)
                {
                    Console.WriteLine("====response 4");
                    _rabbitMqService.SendCancelPaymentMessage(paymentUid.ToString());
                    return NotFound();
                }
            }
            catch
            {
                _rabbitMqService.SendCancelPaymentMessage(paymentUid.ToString());
                return StatusCode(503);
            }
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
                discount = q,
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
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);

            string? content;
            try
            {
                var response = await client.PatchAsync($"http://reservation:8060/api/v1/reservation/{reservationUid}", null);
                content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return NotFound(content);
                }
            }
            catch
            {
                return StatusCode(500);
            }

            var _res = JsonSerializer.Deserialize<reservation>(content);

            try
            {
                var response2 = await client.PatchAsync($"http://payment:8050/api/v1/payment/{_res.paymentUid}", null);
                if (!response2.IsSuccessStatusCode)
                {
                    return NotFound(await response2.Content.ReadAsStringAsync());
                }
            }
            catch
            {
                return StatusCode(500);
            }

            try
            {

                var message = new { username = username };
                var jsonMessage = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonMessage);
                Console.WriteLine($"Sent to rabbit{body}");

                using (var connection = new ConnectionFactory { HostName = "kebnekaise", Port = 5672 }.CreateConnection())
                using (var channel = connection.CreateModel())
                {

                    channel.QueueDeclare(queue: "LoyaltyQueue", durable: true, exclusive: false, autoDelete: false, arguments: null);
                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;

                    channel.BasicPublish(exchange: "", routingKey: "LoyaltyQueue", basicProperties: properties, body: body);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки в RabbitMQ: {ex.Message}");
            }

            // 4. Завершаем запрос
            return NoContent();
        }


        [HttpGet("api/v1/me")]
        public async Task<IActionResult> ReservationMe()
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-User-Name", username);
            reservation?[] content;
            System.Net.Http.HttpResponseMessage response;
            try
            {
                response = await client.GetAsync($"http://reservation:8060/api/v1/reservations");
                content = await response.Content.ReadFromJsonAsync<reservation[]>();
                _circuitBreakerRes.RegisterSuccess();
            }
            catch
            {
                content = null;
                _circuitBreakerRes.RegisterFailure();
            }
            var _hotels = new Dictionary<string, hotel>();
            var _payments = new Dictionary<string, payment>();

            if (content != null)
            foreach (var item in content)
            {
                if (!_hotels.ContainsKey(item.hotelUid.ToString()) && !_circuitBreakerRes.IsOpen())
                {
                    System.Net.Http.HttpResponseMessage response1;
                    try
                    {
                        response1 = await client.GetAsync($"http://reservation:8060/api/v1/hotels/{item.hotelUid}");
                        if (response1.IsSuccessStatusCode)
                        {
                            _circuitBreakerRes.RegisterSuccess();
                            var content1 = await response1.Content.ReadAsStringAsync();
                            _hotels[item.hotelUid.ToString()] = JsonSerializer.Deserialize<hotel>(content1);
                            Console.WriteLine($"    hotelUid: {item.hotelUid}");
                        }
                        else
                        {
                            _circuitBreakerRes.RegisterFailure();
                        }
                    }
                    catch
                    {
                        _circuitBreakerRes.RegisterFailure();
                    }


                }
                if (!_payments.ContainsKey(item.paymentUid.ToString()) && !_circuitBreakerPay.IsOpen())
                {
                    System.Net.Http.HttpResponseMessage response3;
                    try
                    {
                        response3 = await client.GetAsync($"http://payment:8050/api/v1/payment/{item.paymentUid}");
                        Console.WriteLine($"    paymentUUid: {item.paymentUid}");
                        if (!response3.IsSuccessStatusCode)
                        {

                            _circuitBreakerPay.RegisterFailure();
                        }
                        else
                        {
                            _circuitBreakerPay.RegisterSuccess();
                            var content3 = await response3.Content.ReadAsStringAsync();
                            _payments[item.paymentUid.ToString()] = JsonSerializer.Deserialize<payment>(content3);
                        }
                    }
                    catch
                    {
                        _circuitBreakerPay.RegisterFailure();
                        continue;
                    }
                }
            }
            System.Net.Http.HttpResponseMessage response4;
            loyalty? content4 = null ;
            try {
                response4 = await client.GetAsync(
                   $"http://loyalty:8070/api/v1/loyalty");
                content4 = await response4.Content.ReadFromJsonAsync<loyalty>();
                _circuitBreakerLoy.RegisterSuccess();
            } 
            catch {
                _circuitBreakerLoy.RegisterFailure();
            }

                var result = new
                {
                    reservations = content == null? null: content.Select(item => new
                    {
                        reservationUid = item.reservationUid,
                        hotel = _hotels.Count == 0 ? null : new
                        {
                            hotelUid = _hotels[item.hotelUid.ToString()].hotelUid,
                            name = _hotels[item.hotelUid.ToString()].name,
                            fullAddress = _hotels[item.hotelUid.ToString()].country + ", " + _hotels[item.hotelUid.ToString()].city + ", " + _hotels[item.hotelUid.ToString()].address,
                            stars = _hotels[item.hotelUid.ToString()].stars,
                        },

                        startDate = item.startDate.ToString("yyyy-MM-dd"),
                        endDate = item.endDate.ToString("yyyy-MM-dd"),
                        status = item.status,
                       
                        payment = _payments.Count == 0 ? null : new
                        {
                            status = _payments[item.paymentUid.ToString()].status,
                            price = _payments[item.paymentUid.ToString()].price
                        }
                    }).ToList(),
                    loyalty = content4 == null ? new { status = "", discount = 0} : new
                    {
                        status = content4.status,
                        discount = content4.discount
                    }
                };
            return Ok(result);


            return StatusCode((int)response.StatusCode, content);
        }

    }
}