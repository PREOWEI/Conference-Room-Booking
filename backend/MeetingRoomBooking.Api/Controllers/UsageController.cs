using MeetingRoomBooking.Api.DTOs;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;
namespace MeetingRoomBooking.Api.Controllers;

[ApiController, Route("api/usage")]
public class UsageController(UsageService service, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public Task<List<UsageDto>> Get([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return service.Report(from ?? now.AddDays(-30), to ?? now, ct);
    }
}
