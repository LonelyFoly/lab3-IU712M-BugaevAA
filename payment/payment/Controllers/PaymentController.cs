
using payment.DB;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text.Json;
using System.Text;

namespace payment.Controllers
{
   
    [ApiController]

    public class PaymentController : ControllerBase
    {
        
        dbHandler handler;

        private readonly ILogger<PaymentController> _logger;

        public PaymentController(ILogger<PaymentController> logger)
        {
            _logger = logger;
            handler = new dbHandler(null);

        }

        [HttpGet("/manage/health")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }
        [HttpPost("/api/v1/payment")]
        public IActionResult PostPayment([FromBody] PaymentRequestDto request)
        {
            Console.WriteLine($"{request.paymentUid}, {request.price}");
            handler.addPayment(request.paymentUid, request.price);
            return Ok();
        }
        [HttpPatch("/api/v1/payment/{paymentUid}")]
        public IActionResult CancelPayment(Guid paymentUid)
        {
            payment _ = handler.cancelPayment(paymentUid);
            //Console.WriteLine("Payment UID: "+paymentUid.ToString());
            return Ok(_);
        }
        [HttpGet("/api/v1/payment/{paymentUid}")]
        public IActionResult GetPayment(Guid paymentUid)
        {
            payment _ = handler.getPayment(paymentUid);
            //Console.WriteLine("Payment UID: "+paymentUid.ToString());
            return Ok(_);
        }





    }
}
