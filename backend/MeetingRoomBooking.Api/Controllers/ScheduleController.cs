using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController, Route("api/rooms/{roomId:int}/schedule")]
public class ScheduleController(BookingDbContext db, BookingService service) : ControllerBase
{
    [HttpGet]
    public async Task<List<ReservationDto>> Get(int roomId, [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to, CancellationToken ct)
    {
        if (from == default || to <= from)
            throw new BookingException("INVALID_TIME_RANGE", "Choose a valid schedule date range.");
        if (!await db.Rooms.AnyAsync(r => r.Id == roomId, ct))
            throw new BookingException("ROOM_NOT_FOUND", "Room not found.", 404);
        return (await service.List(ct)).Where(r => r.RoomId == roomId && r.Start < to && r.End > from)
            .OrderBy(r => r.Start).ThenBy(r => r.Id).ToList();
    }
}
