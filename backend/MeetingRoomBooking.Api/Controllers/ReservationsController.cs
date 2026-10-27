using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Models;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;
namespace MeetingRoomBooking.Api.Controllers;

[ApiController, Route("api/reservations")]
public class ReservationsController(BookingService service) : ControllerBase
{
    [HttpGet]
    public async Task<List<ReservationDto>> Get([FromQuery] int? roomId, [FromQuery] int? organizerId,
        [FromQuery] ReservationStatus? status, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && to <= from)
            throw new BookingException("INVALID_TIME_RANGE", "Filter end must be after its start.");
        return (await service.List(ct)).Where(r => (!roomId.HasValue || r.RoomId == roomId)
            && (!organizerId.HasValue || r.OrganizerId == organizerId) && (!status.HasValue || r.Status == status)
            && (!from.HasValue || r.End > from) && (!to.HasValue || r.Start < to)).ToList();
    }
    [HttpPost]
    public async Task<IActionResult> Create(BookingRequest request, CancellationToken ct)
    {
        var id = await service.Save(request, null, ct);
        return Created("/api/reservations", new { id });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, BookingRequest request, CancellationToken ct)
    {
        await service.Save(request, id, ct); return NoContent();
    }
    [HttpPost("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, CancellationToken ct) => Act(id, "cancel", ct);
    [HttpPost("{id:int}/check-in")]
    public Task<IActionResult> CheckIn(int id, CancellationToken ct) => Act(id, "check-in", ct);
    [HttpPost("{id:int}/end")]
    public Task<IActionResult> End(int id, CancellationToken ct) => Act(id, "end", ct);
    private async Task<IActionResult> Act(int id, string action, CancellationToken ct)
    {
        await service.Act(id, action, ct); return NoContent();
    }
}
