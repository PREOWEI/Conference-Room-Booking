using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBooking.Api.DTOs;

public class CreateUserRequest
{
    [Required, StringLength(80)] public string FirstName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(80)] public string LastName { get; set; } = "";
}
