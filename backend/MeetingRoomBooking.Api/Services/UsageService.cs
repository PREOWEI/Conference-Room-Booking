using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Services;

public class UsageService(BookingDbContext db, TimeProvider clock)
{
    public async Task<List<UsageDto>> Report(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from)
            throw new BookingException("INVALID_TIME_RANGE", "Report end must be after its start.");

        var now = clock.GetUtcNow();
        var reportCutoff = to < now ? to : now;
        var rooms = await db.Rooms.AsNoTracking().OrderBy(room => room.Name).ToListAsync(ct);
        var reservations = await db.Reservations.AsNoTracking()
            .Where(reservation => reservation.Status != ReservationStatus.Cancelled)
            .ToListAsync(ct);

        return rooms.Select(room =>
        {
            var roomReservations = reservations
                .Where(reservation => reservation.RoomId == room.Id
                    && Minutes(reservation.Start, reservation.End, from, reportCutoff) > 0)
                .ToList();

            var bookedMinutes = roomReservations.Sum(reservation =>
                Minutes(reservation.Start, reservation.End, from, reportCutoff));
            var usedMinutes = roomReservations.Sum(reservation =>
            {
                if (!reservation.CheckedInAt.HasValue)
                    return 0;

                var usageEnd = Min(reservation.ActualEndAt ?? reservation.End, reservation.End);
                return Minutes(reservation.CheckedInAt.Value, usageEnd, from, reportCutoff);
            });

            var checkedInBookings = roomReservations.Count(reservation =>
                reservation.CheckedInAt.HasValue && reservation.CheckedInAt.Value < reportCutoff);
            var noShows = roomReservations.Count(reservation =>
                reservation.End <= reportCutoff && reservation.CheckedInAt == null);
            var usagePercent = bookedMinutes > 0
                ? Math.Round(100 * usedMinutes / bookedMinutes, 1)
                : 0;

            return new UsageDto(
                room.Id,
                room.Name,
                roomReservations.Count,
                checkedInBookings,
                noShows,
                Math.Round(bookedMinutes, 1),
                Math.Round(usedMinutes, 1),
                usagePercent);
        }).ToList();
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second)
        => first < second ? first : second;

    public static double Minutes(DateTimeOffset start, DateTimeOffset end, DateTimeOffset from, DateTimeOffset to)
    {
        var overlapStart = start > from ? start : from;
        var overlapEnd = Min(end, to);
        return Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);
    }
}
