using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MeetingRoomBooking.Api.Controllers;

[ApiController, Route("api/users")]
public class UsersController(BookingDbContext db, TimeProvider clock, Services.BookingGate gate) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)
            || request.FirstName.Length > 80 || request.LastName.Length > 80)
            throw new Services.BookingException("INVALID_NAME", "Enter a first and last name, each up to 80 characters.");
        var email = Services.OrganizerIdentity.NormalizeEmail(request.Email);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                throw new Services.BookingException("EMAIL_EXISTS", "A user with this email already exists. Use it when booking.", 409);
            var user = new OfficeUser { FirstName = Services.OrganizerIdentity.TrimName(request.FirstName), LastName = Services.OrganizerIdentity.TrimName(request.LastName), Email = email };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return Created("/api/users", new UserDto(user.Id, user.FirstName, user.LastName, 0));
        }
        finally { gate.Semaphore.Release(); }
    }

    [HttpGet]
    public async Task<List<UserDto>> Get(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var users = await db.Users.AsNoTracking().OrderBy(x => x.FirstName).ToListAsync(ct);
        var active = await db.Reservations.Where(r => r.Status == ReservationStatus.Booked && r.End > now).ToListAsync(ct);
        return users.Select(u => new UserDto(u.Id, u.FirstName, u.LastName, active.Count(r => r.OrganizerId == u.Id))).ToList();
    }
}
