using MeetingRoomBooking.Api.Configuration;
using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeetingRoomBooking.Api.Services;

public class BookingService(BookingDbContext db, IOptions<BookingOptions> options, TimeProvider clock, BookingGate gate)
{
    public static ReservationStatus EffectiveStatus(Reservation r, DateTimeOffset now) =>
        r.Status == ReservationStatus.Booked && r.End <= now ? ReservationStatus.Completed : r.Status;

    public async Task<List<ReservationDto>> List(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var roomsById = await db.Rooms.ToDictionaryAsync(room => room.Id, ct);
        var usersById = await db.Users.ToDictionaryAsync(user => user.Id, ct);
        var reservations = await db.Reservations.AsNoTracking().ToListAsync(ct);

        return reservations.OrderByDescending(reservation => reservation.Start).Select(reservation =>
        {
            var status = EffectiveStatus(reservation, now);
            var isBooked = status == ReservationStatus.Booked;
            var hasCheckedIn = reservation.CheckedInAt != null;
            var canEdit = isBooked && reservation.Start > now && !hasCheckedIn;
            var canCancel = isBooked && !hasCheckedIn;
            var canCheckIn = isBooked && reservation.Start <= now && !hasCheckedIn;
            var canEnd = isBooked && hasCheckedIn;
            var organizer = usersById[reservation.OrganizerId];

            return new ReservationDto(
                reservation.Id,
                reservation.RoomId,
                roomsById[reservation.RoomId].Name,
                reservation.OrganizerId,
                $"{organizer.FirstName} {organizer.LastName}",
                reservation.Title,
                reservation.Start,
                reservation.End,
                reservation.AttendeeCount,
                status,
                reservation.CheckedInAt,
                reservation.ActualEndAt,
                canEdit,
                canCancel,
                canCheckIn,
                canEnd);
        }).ToList();
    }

    public async Task<int> Save(BookingRequest request, int? id, CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            Reservation? reservation = null;
            if (id.HasValue)
            {
                reservation = await db.Reservations.FindAsync([id.Value], ct)
                    ?? throw new BookingException("RESERVATION_NOT_FOUND", "Reservation not found.", 404);

                var canEdit = EffectiveStatus(reservation, now) == ReservationStatus.Booked
                    && reservation.Start > now
                    && reservation.CheckedInAt == null;
                if (!canEdit)
                    throw new BookingException("INVALID_RESERVATION_STATE", "Only future, unchecked-in bookings can be edited.", 409);
            }

            var room = await db.Rooms.FindAsync([request.RoomId], ct)
                ?? throw new BookingException("ROOM_NOT_FOUND", "Room not found.", 404);
            var organizer = await ResolveOrganizerAsync(request, ct);

            Validate(request, room, now, options.Value);

            var overlappingReservations = await db.Reservations
                .Where(existing => existing.Id != (id ?? 0)
                    && existing.Status == ReservationStatus.Booked
                    && existing.End > now
                    && existing.Start < request.End
                    && request.Start < existing.End)
                .ToListAsync(ct);

            if (overlappingReservations.Any(existing => existing.RoomId == request.RoomId))
                throw new BookingException("ROOM_BOOKING_CONFLICT", "This room is already booked during that time.", 409);

            var organizerHasConflict = organizer.Id != 0
                && overlappingReservations.Any(existing => existing.OrganizerId == organizer.Id);
            if (organizerHasConflict)
                throw new BookingException("ORGANIZER_BOOKING_CONFLICT", "This organizer already has a meeting during that time.", 409);

            if (organizer.Id == 0)
                db.Users.Add(organizer);

            if (reservation == null)
            {
                reservation = new Reservation();
                db.Reservations.Add(reservation);
            }

            reservation.RoomId = request.RoomId;
            reservation.OrganizerId = organizer.Id;
            reservation.Title = request.Title.Trim();
            reservation.Start = request.Start.ToUniversalTime();
            reservation.End = request.End.ToUniversalTime();
            reservation.AttendeeCount = request.AttendeeCount;

            await db.SaveChangesAsync(ct);
            return reservation.Id;
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    private async Task<OfficeUser> ResolveOrganizerAsync(BookingRequest request, CancellationToken ct)
    {
        var hasOrganizerDetails = request.OrganizerFirstName != null
            || request.OrganizerLastName != null
            || request.OrganizerEmail != null;

        // ID-only callers reuse an existing user; the booking form sends name and email.
        if (!hasOrganizerDetails)
        {
            return await db.Users.FindAsync([request.OrganizerId], ct)
                ?? throw new BookingException("ORGANIZER_NOT_FOUND", "Organizer not found.", 404);
        }

        var firstName = OrganizerIdentity.NormalizeName(request.OrganizerFirstName);
        var lastName = OrganizerIdentity.NormalizeName(request.OrganizerLastName);
        if (firstName.Length == 0 || lastName.Length == 0
            || firstName.Length > 80 || lastName.Length > 80)
            throw new BookingException("INVALID_ORGANIZER_NAME", "Enter the organizer's first and last name, each up to 80 characters.");

        var email = OrganizerIdentity.NormalizeEmail(request.OrganizerEmail);
        return await db.Users.SingleOrDefaultAsync(user => user.Email == email, ct)
            ?? new OfficeUser { FirstName = firstName, LastName = lastName, Email = email };
    }

    public static void Validate(BookingRequest r, Room room, DateTimeOffset now, BookingOptions limits)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 120)
            throw new BookingException("INVALID_TITLE", "Enter a meeting title of up to 120 characters.");
        if (r.End <= r.Start)
            throw new BookingException("INVALID_TIME_RANGE", "End must be after start.");
        if (r.Start <= now)
            throw new BookingException("START_NOT_IN_FUTURE", "Start must be in the future.");
        if (r.End - r.Start > TimeSpan.FromDays(limits.MaxDurationDays))
            throw new BookingException("MAX_DURATION_EXCEEDED", $"A meeting cannot exceed {limits.MaxDurationDays} days.");
        if (r.Start > now.AddDays(limits.MaxAdvanceBookingDays))
            throw new BookingException("BOOKING_HORIZON_EXCEEDED", $"Start must be within {limits.MaxAdvanceBookingDays} days.");
        if (r.AttendeeCount < 1)
            throw new BookingException("INVALID_ATTENDEE_COUNT", "Include at least one attendee.");
        if (r.AttendeeCount > room.Capacity)
            throw new BookingException("CAPACITY_EXCEEDED", $"This room holds at most {room.Capacity} people.");
        if (!room.IsActive)
            throw new BookingException("ROOM_INACTIVE", "This room is inactive and cannot be booked.");
    }

    public async Task Act(int id, string action, CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            var r = await db.Reservations.FindAsync([id], ct)
                ?? throw new BookingException("RESERVATION_NOT_FOUND", "Reservation not found.", 404);
            if (EffectiveStatus(r, now) != ReservationStatus.Booked)
                throw new BookingException("INVALID_RESERVATION_STATE", "This reservation is no longer booked.", 409);
            switch (action)
            {
                case "cancel" when r.CheckedInAt == null: r.Status = ReservationStatus.Cancelled; break;
                case "check-in" when r.CheckedInAt == null && r.Start <= now: r.CheckedInAt = now; break;
                case "end" when r.CheckedInAt != null:
                    r.ActualEndAt = now; r.Status = ReservationStatus.Completed; break;
                default: throw new BookingException("INVALID_RESERVATION_STATE", "This action is not available for this meeting.", 409);
            }
            await db.SaveChangesAsync(ct);
        }
        finally { gate.Semaphore.Release(); }
    }
}
