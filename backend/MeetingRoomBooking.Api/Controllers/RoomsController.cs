using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MeetingRoomBooking.Api.Controllers;

[ApiController, Route("api/rooms")]
public class RoomsController(BookingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<RoomDto>> Get([FromQuery] int? minCapacity, [FromQuery] string? floor,
        [FromQuery] string[]? equipment, CancellationToken ct)
    {
        var rows = await db.Rooms.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return rows.Where(x => (!minCapacity.HasValue || x.Capacity >= minCapacity)
            && (string.IsNullOrEmpty(floor) || x.Floor == floor)
            && (equipment == null || equipment.All(e => x.Equipment.Contains(e))))
            .Select(x => new RoomDto(x.Id, x.Name, x.Capacity, x.Floor, x.Equipment, x.IsActive)).ToList();
    }
}
