using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingController(BookingService bookingService) : ControllerBase
{
    private readonly BookingService _bookingService = bookingService;

    private long UserId => long.Parse(User.FindFirst("sub")?.Value ?? "0");
    private long TenantId => long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try
        {
            var booking = await _bookingService.CreateBookingAsync(
                TenantId,
                UserId,
                request.ShiftSlotId,
                request.LocationId,
                request.BookingDate);

            if (booking == null)
                return NotFound(new { error = "Could not create booking" });

            return Ok(booking);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(long id)
    {
        var booking = await _bookingService.GetBookingAsync(TenantId, id);
        if (booking == null)
            return NotFound();

        return Ok(booking);
    }

    [HttpGet]
    public async Task<IActionResult> ListBookings()
    {
        var bookings = await _bookingService.ListBookingsAsync(TenantId, UserId);
        return Ok(bookings);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(long id)
    {
        var success = await _bookingService.CancelBookingAsync(TenantId, id, UserId);
        if (!success)
            return NotFound();

        return Ok(new { message = "Booking cancelled" });
    }
}

public class CreateBookingRequest
{
    public long ShiftSlotId { get; set; }
    public long LocationId { get; set; }
    public DateTime BookingDate { get; set; }
}
