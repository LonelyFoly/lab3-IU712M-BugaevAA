
using reservation.DB;
using Microsoft.AspNetCore.Mvc;

namespace reservation.Controllers
{
    [ApiController]

    public class ReservationController : ControllerBase
    {
        dbHandler handler;
        private readonly ILogger<ReservationController> _logger;

        public ReservationController(ILogger<ReservationController> logger)
        {
            _logger = logger;
            handler = new dbHandler(null);
        }

        [HttpGet("/api/v1/hotels")]
        public async Task<IActionResult> GetHotels([FromQuery] int page, [FromQuery] int size)
        {

            var hotels = handler.getHotels(page, size);
            return Ok(hotels);
        }
        [HttpGet("/api/v1/hotels/{hotel_uuid}")]
        public IActionResult CheckHotel(Guid hotel_uuid)
        {
            var hotel = handler.checkHotel(hotel_uuid);
            if (hotel != null)
                return Ok(hotel);
            else
                return NotFound();
        }
        [HttpGet("/api/v1/reservations")]
        public IActionResult GetReservations()
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is missing.");
            }

            var reservations = handler.getReservationsByUsername(username);
            return Ok(reservations);
        }
        [HttpGet("/api/v1/reservation/{reservationUid}")]
        public IActionResult GetReservations(Guid reservationUid)
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is missing.");
            }

            var reservation = handler.getReservationsByUsernameAndUid(reservationUid,username);
            if (!(reservation.username == ""))
                return Ok(reservation);
            return NotFound();
        }
        [HttpGet("/api/v1/me")]
        public IActionResult GetMe()
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is missing.");
            }
            
            var reservations = handler.getReservationsByUsername(username);

            return Ok(reservations);
        }
        [HttpGet("/manage/health")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }
        [HttpPost("/api/v1/reservation")]
        public IActionResult PostReservation([FromBody] reservation res_)
        {
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                return BadRequest("X-User-Name header is missing.");
            }
            handler.PostReservation(res_, username);
            return NoContent();
        }
        [HttpPatch("/api/v1/reservation/{reservationUid}")]
        public IActionResult CancelReservation(Guid reservationUid)
        {
            Console.WriteLine("G");
            if (!Request.Headers.TryGetValue("X-User-Name", out var username))
            {
                Console.WriteLine("Error: /api/v1/reservation/{reservationUid}");
                return BadRequest("X-User-Name header is missing.");
            }
            reservation res = handler.cancelReservation(reservationUid, username);
            return Ok(res);
        }





    }
}
