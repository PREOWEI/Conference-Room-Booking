using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Api.Models;

namespace MeetingRoomBooking.Api.DTOs;

public class BookingRequest
{
    [Range(1, int.MaxValue)] public int RoomId { get; set; }
    public int OrganizerId { get; set; }
    [StringLength(80)] public string? OrganizerFirstName { get; set; }
    [StringLength(80)] public string? OrganizerLastName { get; set; }
    [StringLength(254)] public string? OrganizerEmail { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    [Range(1, int.MaxValue)] public int AttendeeCount { get; set; }
}

public record ReservationDto(int Id, int RoomId, string Room, int OrganizerId,
    string Organizer, string Title, DateTimeOffset Start, DateTimeOffset End,
    int AttendeeCount, ReservationStatus Status, DateTimeOffset? CheckedInAt,
    DateTimeOffset? ActualEndAt, bool CanEdit, bool CanCancel, bool CanCheckIn, bool CanEnd);
public record UserDto(int Id, string FirstName, string LastName, int ActiveReservations);
public record RoomDto(int Id, string Name, int Capacity, string Floor, string[] Equipment, bool IsActive);
public record UsageDto(int RoomId, string Room, int Bookings, int CheckedInBookings,
    int NoShows, double BookedMinutes, double UsedMinutes, double UsagePercent);
