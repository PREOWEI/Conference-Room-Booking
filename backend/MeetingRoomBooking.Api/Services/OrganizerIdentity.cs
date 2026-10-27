using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBooking.Api.Services;

public static class OrganizerIdentity
{
    public static string TrimName(string? value) => (value ?? "").Trim();

    public static string NormalizeName(string? value)
    {
        var nameParts = TrimName(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", nameParts);
    }

    public static string NormalizeEmail(string? value)
    {
        var email = (value ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            throw new BookingException("INVALID_ORGANIZER_EMAIL", "Enter a valid organizer email address.");
        return email;
    }
}
