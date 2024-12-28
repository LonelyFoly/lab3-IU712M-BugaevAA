using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using gateway.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using gateway;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HotelService.UnitTests
{
    
    public class GatewayControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<RabbitMqService> _rbtMock;
        private readonly GatewayController _controller;

        public GatewayControllerTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _rbtMock = new Mock<RabbitMqService>();
            _controller = new GatewayController(_httpClientFactoryMock.Object, _rbtMock.Object);
        }

        private HttpClient CreateMockHttpClient(HttpResponseMessage response)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            return new HttpClient(handlerMock.Object);
        }

        private HttpResponseMessage GetHotelApiResponse()
        {
            var responseContent = new List<hotel>
        {
            new hotel
            {
                id = 1,
                name = "Ararat Park Hyatt Moscow",
                city = "Москва",
                country = "Россия",
                address = "Неглинная ул., 4",
                hotelUid = Guid.Parse("049161bb-badd-4fa8-9d90-87c9a82b0668"),
                price = 10000,
                stars = 5
            }
        };

            var json = JsonSerializer.Serialize(responseContent);
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            };
        }
        [Fact]
        public void CheckHealth()
        {

            var result = _controller.CheckHealth();
            Assert.IsType<OkResult>(result);
        }
        private class ProxyReservationResponse
        {
            public int page { get; set; }
            public int pageSize { get; set; }
            public int totalElements { get; set; }
            public List<hotel> items { get; set; }
        }
        [Fact]
        public async Task GetHotels()
        {

            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(CreateMockHttpClient(GetHotelApiResponse()));

            int page = 1;
            int size = 10;

            var result = await _controller.ProxyReservationService(page, size);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonString = JsonSerializer.Serialize(okResult.Value);

            var response = JsonSerializer.Deserialize<ProxyReservationResponse>(jsonString);
            Assert.NotNull(response);
            Assert.Equal(page, response.page);
            Assert.Equal(size, response.pageSize);
            Assert.Equal(1, response.totalElements);
            Assert.Single(response.items);

            var firstHotel = response.items[0];
            Assert.Equal(1, firstHotel.id);
            Assert.Equal("Ararat Park Hyatt Moscow", firstHotel.name);
            Assert.Equal("Москва", firstHotel.city);
            Assert.Equal("Россия", firstHotel.country);
            Assert.Equal("Неглинная ул., 4", firstHotel.address);
            Assert.Equal("049161bb-badd-4fa8-9d90-87c9a82b0668", firstHotel.hotelUid.ToString());
            Assert.Equal(10000, firstHotel.price);
            Assert.Equal(5, firstHotel.stars);
        }
        [Fact]
        public async Task GetLoyalty()
        {
            var loyaltyResponse = new loyalty
            {
                status = "GOLD",
                discount = 10,
                reservationCount = 25
            };
            var jsonResponse = JsonSerializer.Serialize(loyaltyResponse);
            var httpResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };

            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(CreateMockHttpClient(httpResponse));
            var result = await _controller.GetLoyalty();
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = okResult.Value;
            Assert.Equal("GOLD", resultValue.GetType().GetProperty("status").GetValue(resultValue));
            Assert.Equal(10, resultValue.GetType().GetProperty("discount").GetValue(resultValue));
            Assert.Equal(25, resultValue.GetType().GetProperty("reservationCount").GetValue(resultValue));
        }
        







    }



}