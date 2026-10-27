using MeetingRoomBooking.Api.Configuration;
using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Models;
using MeetingRoomBooking.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using MeetingRoomBooking.Api.Controllers;

namespace MeetingRoomBooking.Tests;

public class BookingTests : IDisposable
{
    private readonly FixedClock clock = new();
    private readonly BookingDbContext db;
    private readonly BookingOptions limits = new();
    private readonly BookingService service;
    public BookingTests()
    {
        db = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Rooms.AddRange(new Room
        {
            Id = 1,
            Name = "A",
            Capacity = 8
        }, new Room
        {
            Id = 2,
            Name = "B",
            Capacity = 4
        }, new Room
        {
            Id = 3,
            Name = "Closed",
            Capacity = 8,
            IsActive = false
        });
        db.Users.AddRange(new OfficeUser { Id = 1 }, new OfficeUser { Id = 2 });
        db.SaveChanges();
        service = new BookingService(db, Options.Create(limits), clock, new BookingGate());
    }
    private BookingRequest Request() => new()
    {
        RoomId = 1,
        OrganizerId = 1,
        Title = "Planning",
        Start = clock.Now.AddDays(1),
        End = clock.Now.AddDays(1).AddHours(1),
        AttendeeCount = 4
    };
    private async Task Expect(string code, BookingRequest request)
    {
        // Act
        var exception = await Assert.ThrowsAsync<BookingException>(() => service.Save(request, null, default));

        // Assert
        Assert.Equal(code, exception.Code);
    }

    [Fact]
    public async Task ValidBookingIsSaved()
    {
        // Arrange — shared fixture provides the database, clock, and service.

        // Act
        var id = await service.Save(Request(), null, default);

        // Assert
        Assert.Equal("Planning", (await db.Reservations.FindAsync(id))!.Title);
    }

    [Fact]
    public async Task ExactlySevenDaysAllowed()
    {
        // Arrange
        var r = Request();
        r.End = r.Start.AddDays(7);

        // Act
        await service.Save(r, null, default);

        // Assert: completing without an exception confirms the booking is allowed.
    }

    [Fact]
    public async Task LongerThanSevenDaysRejected()
    {
        // Arrange
        var r = Request();
        r.End = r.Start.AddDays(7).AddTicks(1);

        // Act and Assert
        await Expect("MAX_DURATION_EXCEEDED", r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidRangeRejected(int minutes)
    {
        // Arrange
        var r = Request();
        r.End = r.Start.AddMinutes(minutes);

        // Act and Assert
        await Expect("INVALID_TIME_RANGE", r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonFutureStartRejected(int minutes)
    {
        // Arrange
        var r = Request();
        r.Start = clock.Now.AddMinutes(minutes);

        // Act and Assert
        await Expect("START_NOT_IN_FUTURE", r);
    }

    [Fact]
    public async Task RoomOverlapRejected()
    {
        // Arrange
        await service.Save(Request(), null, default);
        var r = Request();
        r.OrganizerId = 2;

        // Act and Assert
        await Expect("ROOM_BOOKING_CONFLICT", r);
    }

    [Fact]
    public async Task OrganizerOverlapAcrossRoomsRejected()
    {
        // Arrange
        await service.Save(Request(), null, default);
        var r = Request();
        r.RoomId = 2;

        // Act and Assert
        await Expect("ORGANIZER_BOOKING_CONFLICT", r);
    }

    [Fact]
    public async Task AdjacentMeetingsAllowed()
    {
        // Arrange
        var r = Request();
        await service.Save(r, null, default);
        r.Start = r.End;
        r.End = r.Start.AddHours(1);

        // Act
        await service.Save(r, null, default);

        // Assert: completing without an exception confirms the booking is allowed.
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed)]
    public async Task NonBookedReservationsDoNotBlock(ReservationStatus status)
    {
        // Arrange
        var id = await service.Save(Request(), null, default);
        (await db.Reservations.FindAsync(id))!.Status = status;
        await db.SaveChangesAsync();

        // Act
        await service.Save(Request(), null, default);

        // Assert: completing without an exception confirms the booking is allowed.
    }

    [Fact]
    public async Task ExactCapacityAllowed()
    {
        // Arrange
        var r = Request();
        r.AttendeeCount = 8;

        // Act
        await service.Save(r, null, default);

        // Assert: completing without an exception confirms the booking is allowed.
    }

    [Fact]
    public async Task OverCapacityRejected()
    {
        // Arrange
        var r = Request();
        r.AttendeeCount = 9;

        // Act and Assert
        await Expect("CAPACITY_EXCEEDED", r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidAttendeesRejected(int count)
    {
        // Arrange
        var r = Request();
        r.AttendeeCount = count;

        // Act and Assert
        await Expect("INVALID_ATTENDEE_COUNT", r);
    }

    [Fact]
    public async Task ExactHorizonAllowed()
    {
        // Arrange
        var r = Request();
        r.Start = clock.Now.AddDays(90);
        r.End = r.Start.AddHours(1);

        // Act
        await service.Save(r, null, default);

        // Assert: completing without an exception confirms the booking is allowed.
    }

    [Fact]
    public async Task BeyondHorizonRejected()
    {
        // Arrange
        var r = Request();
        r.Start = clock.Now.AddDays(90).AddTicks(1);
        r.End = r.Start.AddHours(1);

        // Act and Assert
        await Expect("BOOKING_HORIZON_EXCEEDED", r);
    }

    [Fact]
    public async Task InactiveRoomRejected()
    {
        // Arrange
        var r = Request();
        r.RoomId = 3;

        // Act and Assert
        await Expect("ROOM_INACTIVE", r);
    }

    [Fact]
    public async Task MissingRoomRejected()
    {
        // Arrange
        var r = Request();
        r.RoomId = 99;

        // Act and Assert
        await Expect("ROOM_NOT_FOUND", r);
    }

    [Fact]
    public async Task MissingOrganizerRejected()
    {
        // Arrange
        var r = Request();
        r.OrganizerId = 99;

        // Act and Assert
        await Expect("ORGANIZER_NOT_FOUND", r);
    }

    [Fact]
    public async Task ConfigurationControlsDuration()
    {
        // Arrange
        limits.MaxDurationDays = 1;
        var r = Request();
        r.End = r.Start.AddDays(2);

        // Act and Assert
        await Expect("MAX_DURATION_EXCEEDED", r);
    }

    [Fact]
    public async Task ConfigurationControlsHorizon()
    {
        // Arrange
        limits.MaxAdvanceBookingDays = 1;
        var r = Request();
        r.Start = clock.Now.AddDays(2);
        r.End = r.Start.AddHours(1);

        // Act and Assert
        await Expect("BOOKING_HORIZON_EXCEEDED", r);
    }

    [Fact]
    public async Task EditExcludesItself()
    {
        // Arrange
        var r = Request();
        var id = await service.Save(r, null, default);
        r.Title = "Changed";

        // Act
        await service.Save(r, id, default);

        // Assert
        Assert.Single(db.Reservations);
    }

    [Fact]
    public async Task EditChecksOtherBookings()
    {
        // Arrange
        var r = Request();
        var id = await service.Save(r, null, default);
        var other = Request();
        other.Start = r.Start.AddHours(2);
        other.End = other.Start.AddHours(1);
        await service.Save(other, null, default);
        r.Start = other.Start;
        r.End = other.End;

        // Act and Assert
        Assert.Equal("ROOM_BOOKING_CONFLICT", (await Assert.ThrowsAsync<BookingException>(() => service.Save(r, id, default))).Code);
    }

    [Fact]
    public async Task CancelReleasesBooking()
    {
        // Arrange
        var id = await service.Save(Request(), null, default);

        // Act
        await service.Act(id, "cancel", default);

        // Assert
        await service.Save(Request(), null, default);
    }

    [Fact]
    public async Task CheckInBeforeStartRejected()
    {
        // Arrange
        var id = await service.Save(Request(), null, default);

        // Act and Assert
        await Assert.ThrowsAsync<BookingException>(() => service.Act(id, "check-in", default));
    }

    [Fact]
    public async Task CannotEndWithoutCheckIn()
    {
        // Arrange
        var id = await service.Save(Request(), null, default);
        clock.Now = Request().Start;

        // Act and Assert
        await Assert.ThrowsAsync<BookingException>(() => service.Act(id, "end", default));
    }

    [Fact]
    public async Task EarlyEndPreservesScheduleAndReleasesRoom()
    {
        // Arrange
        var r = Request();
        var id = await service.Save(r, null, default);
        clock.Now = r.Start;

        // Act
        await service.Act(id, "check-in", default);

        // Assert
        await Assert.ThrowsAsync<BookingException>(() => service.Act(id, "check-in", default));
        await Assert.ThrowsAsync<BookingException>(() => service.Act(id, "cancel", default));

        // Arrange and Act — end the checked-in meeting early.
        clock.Now = r.Start.AddMinutes(15);
        await service.Act(id, "end", default);

        // Assert — keep the scheduled end for reporting.
        var saved = (await db.Reservations.FindAsync(id))!;
        Assert.Equal(r.End, saved.End);
        Assert.Equal(clock.Now, saved.ActualEndAt);
        Assert.Equal(ReservationStatus.Completed, saved.Status);

        // Act and Assert — the released room can be booked again.
        r.Start = clock.Now.AddMinutes(1);
        await service.Save(r, null, default);
    }

    [Fact]
    public async Task ExpiredBookingAppearsCompleted()
    {
        // Arrange
        var r = Request();
        await service.Save(r, null, default);

        // Act
        clock.Now = r.End;

        // Assert
        Assert.Equal(ReservationStatus.Completed, (await service.List(default)).Single().Status);
    }

    [Fact]
    public async Task UsageRecordsEarlyEndingAndNoShow()
    {
        // Arrange
        db.Reservations.AddRange(
            new Reservation
            {
                RoomId = 1,
                OrganizerId = 1,
                Start = clock.Now.AddHours(-2),
                End = clock.Now.AddHours(-1),
                CheckedInAt = clock.Now.AddHours(-2),
                ActualEndAt = clock.Now.AddMinutes(-90),
                Status = ReservationStatus.Completed
            },
            new Reservation
            {
                RoomId = 1,
                OrganizerId = 1,
                Start = clock.Now.AddHours(-4),
                End = clock.Now.AddHours(-3)
            });
        await db.SaveChangesAsync();

        // Act
        var report = await new UsageService(db, clock).Report(clock.Now.AddDays(-1), clock.Now.AddDays(1), default);
        var row = report.Single(r => r.RoomId == 1);

        // Assert
        Assert.Equal(120, row.BookedMinutes);
        Assert.Equal(30, row.UsedMinutes);
        Assert.Equal(25, row.UsagePercent);
        Assert.Equal(1, row.NoShows);
        Assert.Equal(0, report.Single(r => r.RoomId == 2).UsagePercent);
    }

    [Fact]
    public async Task OngoingUsageExcludesFutureAndCancelledTime()
    {
        // Arrange
        db.Reservations.AddRange(
            new Reservation
            {
                RoomId = 1,
                Start = clock.Now.AddMinutes(-30),
                End = clock.Now.AddMinutes(30),
                CheckedInAt = clock.Now.AddMinutes(-15)
            },
            new Reservation
            {
                RoomId = 1,
                Start = clock.Now.AddHours(-2),
                End = clock.Now.AddHours(-1),
                Status = ReservationStatus.Cancelled
            });
        await db.SaveChangesAsync();

        // Act
        var row = (await new UsageService(db, clock).Report(clock.Now.AddDays(-1), clock.Now.AddDays(1), default)).Single(r => r.RoomId == 1);

        // Assert
        Assert.Equal(30, row.BookedMinutes);
        Assert.Equal(15, row.UsedMinutes);
        Assert.Equal(50, row.UsagePercent);
        Assert.Equal(0, row.NoShows);
    }

    [Fact]
    public void UsageClipsToReportWindow()
    {
        // Arrange — shared fixture provides the database, clock, and service.

        // Act and Assert
        Assert.Equal(15, UsageService.Minutes(clock.Now.AddHours(-1), clock.Now.AddHours(1), clock.Now.AddMinutes(-15), clock.Now));
    }

    [Fact]
    public async Task UserCountsExcludeCancelledAndElapsedBookings()
    {
        // Arrange
        await service.Save(Request(), null, default);
        db.Reservations.AddRange(
            new Reservation
            {
                RoomId = 2,
                OrganizerId = 1,
                Start = clock.Now.AddDays(2),
                End = clock.Now.AddDays(2).AddHours(1),
                Status = ReservationStatus.Cancelled
            },
            new Reservation
            {
                RoomId = 2,
                OrganizerId = 1,
                Start = clock.Now.AddHours(-2),
                End = clock.Now.AddHours(-1)
            });
        await db.SaveChangesAsync();

        // Act
        var users = await new UsersController(db, clock, new BookingGate()).Get(default);

        // Assert
        Assert.Equal(1, users.Single(u => u.Id == 1).ActiveReservations);
        Assert.Equal(0, users.Single(u => u.Id == 2).ActiveReservations);
    }

    [Fact]
    public async Task EquipmentFiltersRequireEverySelectedItem()
    {
        // Arrange
        (await db.Rooms.FindAsync(1))!.Equipment = ["Projector", "Whiteboard"];
        (await db.Rooms.FindAsync(2))!.Equipment = ["Projector"];
        await db.SaveChangesAsync();

        // Act
        var rooms = await new RoomsController(db).Get(null, null, ["Projector", "Whiteboard"], default);

        // Assert
        Assert.Equal(1, Assert.Single(rooms).Id);
    }

    [Fact]
    public async Task NewPersonCanOrganizeBooking()
    {
        // Arrange — shared fixture provides the database, clock, and service.

        // Act
        await new UsersController(db, clock, new BookingGate()).Create(new CreateUserRequest
        {
            FirstName = "  Ada  ",
            LastName = "  Obi  ",
            Email = "ada@example.com"
        }, default);
        var person = await db.Users.SingleAsync(u => u.FirstName == "Ada");

        // Assert
        Assert.Equal("Obi", person.LastName);

        // Arrange and Act — book for the newly created person.
        var r = Request();
        r.OrganizerId = person.Id;
        await service.Save(r, null, default);

        // Assert
        Assert.Equal("Ada Obi", (await service.List(default)).Single().Organizer);
    }

    [Theory]
    [InlineData("", "Obi")]
    [InlineData("Ada", "  ")]
    public async Task BlankPersonNameRejected(string first, string last)
    {
        // Arrange — shared fixture provides the database, clock, and service.

        // Act and Assert
        await Assert.ThrowsAsync<BookingException>(() => new UsersController(db, clock, new BookingGate())
            .Create(new CreateUserRequest
            {
                FirstName = first,
                LastName = last
            }, default));
        Assert.Equal(2, await db.Users.CountAsync());
    }


    private BookingRequest EmailRequest()
    {
        var r = Request();
        r.OrganizerId = 0;
        r.OrganizerFirstName = "Ada";
        r.OrganizerLastName = "Obi";
        r.OrganizerEmail = "ada@example.com";
        return r;
    }

    [Fact]
    public async Task ReturningEmailReusesUserForAnotherBooking()
    {
        // Arrange
        var r = EmailRequest();
        await service.Save(r, null, default);
        r.Start = r.End;
        r.End = r.Start.AddHours(1);
        r.OrganizerEmail = " ADA@EXAMPLE.COM ";

        // Act
        await service.Save(r, null, default);

        // Assert
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Single((await service.List(default)).Select(x => x.OrganizerId).Distinct());
        var users = await new UsersController(db, clock, new BookingGate()).Get(default);
        Assert.Equal(2, users.Single(u => u.FirstName == "Ada").ActiveReservations);
    }

    [Fact]
    public async Task SameEmailAndChangedNameStillRejectsOverlap()
    {
        // Arrange
        var r = EmailRequest();
        await service.Save(r, null, default);
        r.RoomId = 2;
        r.OrganizerLastName = "Obii";
        r.OrganizerEmail = "ADA@example.com";

        // Act and Assert
        await Expect("ORGANIZER_BOOKING_CONFLICT", r);
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task SameNameWithDifferentEmailsGetsSeparateIds()
    {
        // Arrange
        var r = EmailRequest();
        await service.Save(r, null, default);
        r.RoomId = 2;
        r.OrganizerEmail = "other.ada@example.com";

        // Act
        await service.Save(r, null, default);

        // Assert
        Assert.Equal(2, (await service.List(default)).Select(x => x.OrganizerId).Distinct().Count());
    }

    [Fact]
    public async Task InvalidBookingDoesNotCreateUser()
    {
        // Arrange
        var r = EmailRequest();
        r.AttendeeCount = 99;

        // Act and Assert
        await Expect("CAPACITY_EXCEEDED", r);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task OrganizerEmailRequired(string? email)
    {
        // Arrange
        var r = EmailRequest();
        r.OrganizerEmail = email;

        // Act and Assert
        await Expect("INVALID_ORGANIZER_EMAIL", r);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DuplicateUserRegistrationRejectedButBookingAllowed()
    {
        // Arrange
        var controller = new UsersController(db, clock, new BookingGate());
        await controller.Create(new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Obi",
            Email = "ada@example.com"
        }, default);

        // Act
        var exception = await Assert.ThrowsAsync<BookingException>(() => controller.Create(
            new CreateUserRequest
            {
                FirstName = "Ada",
                LastName = "Obi",
                Email = " ADA@EXAMPLE.COM "
            }, default));

        // Assert
        Assert.Equal("EMAIL_EXISTS", exception.Code);

        // Act — an existing email can still be used for a booking.
        await service.Save(EmailRequest(), null, default);

        // Assert
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task EditKeepsExistingEmailIdentity()
    {
        // Arrange
        var r = EmailRequest();
        var id = await service.Save(r, null, default);
        r.Title = "Changed";

        // Act
        await service.Save(r, id, default);

        // Assert
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task PublicListsDoNotExposeEmail()
    {
        // Arrange
        await service.Save(EmailRequest(), null, default);

        // Act
        var users = await new UsersController(db, clock, new BookingGate()).Get(default);
        var userJson = System.Text.Json.JsonSerializer.Serialize(users);
        var bookingJson = System.Text.Json.JsonSerializer.Serialize(await service.List(default));

        // Assert
        Assert.DoesNotContain("ada@example.com", userJson);
        Assert.DoesNotContain("ada@example.com", bookingJson);
        Assert.DoesNotContain("Email", userJson);
        Assert.DoesNotContain("Email", bookingJson);
    }

    [Fact]
    public async Task DailyScheduleIncludesOvernightMeetingsAndExcludesOtherRooms()
    {
        // Arrange
        var day = clock.Now.AddDays(1);
        db.Reservations.AddRange(
            new Reservation
            {
                RoomId = 1,
                OrganizerId = 1,
                Title = "Later",
                Start = day.AddHours(2),
                End = day.AddHours(3)
            },
            new Reservation
            {
                RoomId = 1,
                OrganizerId = 1,
                Title = "Overnight",
                Start = day.AddHours(-2),
                End = day.AddHours(1)
            },
            new Reservation
            {
                RoomId = 2,
                OrganizerId = 1,
                Title = "Other room",
                Start = day,
                End = day.AddHours(1)
            },
            new Reservation
            {
                RoomId = 1,
                OrganizerId = 1,
                Title = "Outside",
                Start = day.AddDays(1),
                End = day.AddDays(1).AddHours(1)
            });
        await db.SaveChangesAsync();

        // Act
        var result = await new ScheduleController(db, service).Get(1, day, day.AddDays(1), default);

        // Assert
        Assert.Equal(new[] { "Overnight", "Later" }, result.Select(r => r.Title));
    }
    public void Dispose() => db.Dispose();
    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
