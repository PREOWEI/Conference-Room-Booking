using MeetingRoomBooking.Api.Models;

namespace MeetingRoomBooking.Api.Data;

public static class Seeder
{
    public static void Seed(BookingDbContext db, DateTimeOffset now)
    {
        if (db.Rooms.Any()) return;
        db.Rooms.AddRange(
            new Room { Id = 1, Name = "Conference Room 1", Capacity = 8, Floor = "1", Equipment = ["TV screen", "Whiteboard"] },
            new Room { Id = 2, Name = "Conference Room 2", Capacity = 4, Floor = "1", Equipment = ["Video conferencing"] },
            new Room { Id = 3, Name = "Conference Room 3", Capacity = 16, Floor = "2", Equipment = ["Projector", "Whiteboard", "Video conferencing"] },
            new Room { Id = 4, Name = "Conference Room 4", Capacity = 6, Floor = "2", Equipment = ["TV screen"] },
            new Room { Id = 5, Name = "Conference Room 5", Capacity = 10, Floor = "3", Equipment = ["Projector", "Whiteboard"], IsActive = false });
        db.Users.AddRange(
            new OfficeUser { Id = 1, FirstName = "Chinedu", LastName = "Okafor", Email = "chinedu.okafor@example.com" },
            new OfficeUser { Id = 2, FirstName = "Amina", LastName = "Bello", Email = "amina.bello@example.com" },
            new OfficeUser { Id = 3, FirstName = "Tunde", LastName = "Adeyemi", Email = "tunde.adeyemi@example.com" },
            new OfficeUser { Id = 4, FirstName = "Ngozi", LastName = "Eze", Email = "ngozi.eze@example.com" });
        db.Reservations.AddRange(
            new Reservation { RoomId = 1, OrganizerId = 1, Title = "Team planning", Start = now.AddDays(1), End = now.AddDays(1).AddHours(1), AttendeeCount = 6 },
            new Reservation { RoomId = 2, OrganizerId = 2, Title = "Ready for check-in", Start = now.AddMinutes(-10), End = now.AddMinutes(50), AttendeeCount = 3 },
            new Reservation { RoomId = 3, OrganizerId = 3, Title = "Design review", Start = now.AddMinutes(-20), End = now.AddMinutes(40), CheckedInAt = now.AddMinutes(-15), AttendeeCount = 8 },
            new Reservation { RoomId = 1, OrganizerId = 1, Title = "Weekly review", Start = now.AddDays(-1), End = now.AddDays(-1).AddHours(1), CheckedInAt = now.AddDays(-1).AddMinutes(5), ActualEndAt = now.AddDays(-1).AddMinutes(40), Status = ReservationStatus.Completed, AttendeeCount = 5 },
            new Reservation { RoomId = 4, OrganizerId = 4, Title = "Unattended booking", Start = now.AddDays(-2), End = now.AddDays(-2).AddHours(2), Status = ReservationStatus.Completed, AttendeeCount = 2 },
            new Reservation { RoomId = 2, OrganizerId = 2, Title = "Cancelled catch-up", Start = now.AddDays(2), End = now.AddDays(2).AddHours(1), Status = ReservationStatus.Cancelled, AttendeeCount = 2 });
        db.SaveChanges();
    }
}
