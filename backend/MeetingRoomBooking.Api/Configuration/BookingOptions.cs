namespace MeetingRoomBooking.Api.Configuration;

public class BookingOptions
{
    public int MaxDurationDays { get; set; } = 7;
    public int MaxAdvanceBookingDays { get; set; } = 90;
}
