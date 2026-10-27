using System.Text.Json.Serialization;
using MeetingRoomBooking.Api.Configuration;
using MeetingRoomBooking.Api.Data;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState) { Status = 400, Title = "Check the submitted fields." };
    problem.Extensions["code"] = "INVALID_REQUEST";
    return new BadRequestObjectResult(problem);
});
builder.Services.AddDbContext<BookingDbContext>(o => o.UseInMemoryDatabase("MeetingRooms"));
builder.Services.AddOptions<BookingOptions>().BindConfiguration("BookingRules")
    .Validate(o => o.MaxDurationDays > 0 && o.MaxAdvanceBookingDays > 0, "Booking limits must be positive.").ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BookingGate>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Unexpected failures are logged by the framework and return a generic response.
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
    Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "An unexpected error occurred.",
        detail: "Please try again later.",
        extensions: new Dictionary<string, object?> { ["code"] = "INTERNAL_SERVER_ERROR" })
        .ExecuteAsync(context)));

app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (BookingException e)
    {
        await Results.Problem(statusCode: e.Status, title: "Request could not be completed", detail: e.Message,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code }).ExecuteAsync(context);
    }
});
using (var scope = app.Services.CreateScope())
    Seeder.Seed(scope.ServiceProvider.GetRequiredService<BookingDbContext>(), TimeProvider.System.GetUtcNow());

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
