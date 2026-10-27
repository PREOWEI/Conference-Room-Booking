namespace MeetingRoomBooking.Api.Models;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public string Floor { get; set; } = "";
    public string[] Equipment { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public class OfficeUser
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public enum ReservationStatus { Booked, Completed, Cancelled }

public class Reservation
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int OrganizerId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int AttendeeCount { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? ActualEndAt { get; set; }
}
