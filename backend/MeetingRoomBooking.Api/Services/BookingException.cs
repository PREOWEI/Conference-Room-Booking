namespace MeetingRoomBooking.Api.Services;

public class BookingException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

// Serializes participating validation and writes within this process.
// This is not a database transaction and does not provide rollback or multi-server protection.
public sealed class BookingGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}
