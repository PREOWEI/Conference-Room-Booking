# Conference Room Booking

A small office meeting-room booking application built with C#, ASP.NET Core, EF Core InMemory, React, and TypeScript. The interface uses Bootstrap and plain tables.

## Run locally

Install the .NET 9 SDK and Node.js 22 or a compatible newer version. Open two terminals in this project folder.

**Terminal 1 — backend**

```powershell
cd backend/MeetingRoomBooking.Api
dotnet run
```

**Terminal 2 — frontend**

```powershell
cd frontend
npm.cmd install
npm.cmd run dev
```

Open the Local address printed by Vite (normally http://localhost:5173). Keep both terminals running. Stop them with Ctrl+C. On macOS/Linux, use `npm` instead of `npm.cmd`.

The frontend proxy reads the backend's existing HTTP URL from `Properties/launchSettings.json`; no custom port is needed. Start the backend using its default HTTP profile. Data is seeded automatically and resets whenever the backend restarts.

## Verify

From the project root:

```powershell
dotnet test
```

From the frontend folder:

```powershell
npm.cmd run build
```

## Features

- Rooms: ID, capacity, floor, equipment, active status; minimum-capacity, floor, and equipment filters. Multiple selected equipment options must all match.
- Users: ID, names, backend-calculated active reservation counts. Enter the organizer’s first name, last name, and email. A new email creates a user only after the booking passes validation. Returning users enter the same email to reuse their existing user ID.
- Reservations: room, organizer, title, start/end, attendees, status; room, organizer, status, and date filters.
- Create and edit future bookings, cancel, check in, and end a checked-in meeting early.
- Room usage report with date filters, booked/used minutes, recorded check-ins, no-shows, and usage percentage.
- Server-side validation with human-readable messages and stable error codes.

## Organizer identity and email

Email identifies the organizer; it is not a login or an email notification feature.

- Enter first name, last name, and email when creating or editing a reservation.
- A returning organizer enters the same email to reuse their existing user ID and stored name.
- Email matching ignores leading/trailing spaces and letter case.
- Two people with the same name can have separate user IDs when they use different emails.
- The same organizer cannot have overlapping bookings, even in different rooms.
- A new user is saved only after their booking passes validation. The separate user-registration endpoint rejects duplicate emails; repeat bookings with an existing email are allowed.
- Emails are stored internally and omitted from the Users and Reservations list responses and tables.
- Email addresses are not verified, and no confirmation emails are sent. A mistyped email can create a different user. Knowing an email does not prove a person's identity; authentication is outside this assignment.
- The API also supports an existing OrganizerId. The current frontend uses name and email, so both input paths are retained.

Example: Ada Obi with ada@example.com reuses the same user on her next booking. Another Ada Obi with ada.other@example.com receives a separate user ID. These are example addresses only.

All users and bookings are held in memory. Restarting the backend restores the seeded sample data.

## Booking rules

`backend/MeetingRoomBooking.Api/appsettings.json` contains `BookingRules:MaxDurationDays` (7) and `BookingRules:MaxAdvanceBookingDays` (90). Change these and restart the backend to use different limits.

- Exactly 7 days is allowed; longer is rejected.
- Start must be strictly in the future; end must follow start.
- Exactly 90 days ahead is allowed. Only the start is constrained by this horizon.
- Both room and organizer must be free. Overlap means `existing.Start < requested.End && requested.Start < existing.End`, so adjacent meetings are allowed.
- Cancelled and completed meetings do not block new bookings.
- Attendees must be positive and cannot exceed capacity. Include the organizer in the count.
- Inactive rooms cannot be booked.
- Room and organizer must exist; title must contain text and be at most 120 characters.

## Usage assumptions and lifecycle

Actual usage is recorded through **manual check-in and meeting ending**, not room sensors. This is an explicit assumption because the assignment does not define occupancy collection. A check-in records reported use, not proof of physical attendance.

A reservation begins as Booked. Check-in is allowed during its scheduled interval. Before check-in, a Booked reservation can be cancelled. After check-in, it can be ended, recording actual end time and becoming Completed. Ending early leaves the scheduled start/end unchanged and releases the remaining reserved time.

An elapsed Booked reservation is presented as Completed by backend logic. This effective status avoids needing a scheduler for the demo. All listing, filtering, counts, action checks, and overlap checks respect the scheduled end. The stored enum may remain Booked after natural expiration; clients always receive the effective status.

Usage reports:

- Exclude cancelled reservations and future time.
- Include bookings with a positive scheduled overlap with the elapsed report interval.
- Clip both booked and used minutes to that interval.
- Count recorded usage from check-in to actual end, or scheduled end if not explicitly ended; ongoing usage stops at server time.
- Count a no-show only if its scheduled end has passed within the report cutoff and no check-in was recorded.
- Calculate usage as used minutes / booked minutes × 100; return zero when no minutes were booked.
- Preserve the originally booked time after early ending.

Dates are stored/compared as UTC `DateTimeOffset`. The browser displays local time and sends explicit offsets. Date-range filters use local midnight through the next midnight after the selected end date, so the end date is inclusive even across daylight-saving changes.

## Architecture

One API project is enough for this assignment:

- `Models`: stored records.
- `DTOs`: request and response shapes.
- `Data`: EF Core database and sample records.
- `Services`: booking validation, state changes, and report calculations.
- `Controllers`: HTTP endpoints.
- `Configuration`: typed booking limits.

Built-in dependency injection supplies the DbContext, services, configuration, and clock. DTOs use small manual mappings. `TimeProvider` makes time-dependent rules testable. A shared semaphore protects booking validation and writes against concurrent requests in this single process. It does not provide multi-server guarantees.

React uses simple component state, a shared fetch helper, and five hash-linked pages. There is no authentication, Redux, repository framework, or extra service layer. Action permissions are returned by the backend and enforced again when an action is submitted. Visible data refreshes every minute and after mutations.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/rooms/{id}/schedule` | Chronological meetings overlapping `from` through exclusive `to` |
| GET | `/api/rooms` | Filter rooms by `minCapacity`, `floor`, repeated `equipment` |
| GET | `/api/users` | Users with active booking counts |
| POST | `/api/users` | Add a person using first name, last name, and a unique email |
| GET | `/api/reservations` | Filter by `roomId`, `organizerId`, `status`, `from`, `to` |
| POST | `/api/reservations` | Create |
| PUT | `/api/reservations/{id}` | Edit future unchecked-in booking |
| POST | `/api/reservations/{id}/cancel` | Cancel |
| POST | `/api/reservations/{id}/check-in` | Check in |
| POST | `/api/reservations/{id}/end` | End meeting |
| GET | `/api/usage` | Usage report with `from`, `to` |

OpenAPI JSON is available at `/openapi/v1.json` during development. Business errors use ProblemDetails with a `code` and readable `detail`; malformed request fields use ValidationProblemDetails with `INVALID_REQUEST`. Validation returns 400, missing records 404, and conflicts/state errors 409.

## Quick demonstration

1. Open Rooms. Try minimum capacity 8 and Whiteboard.
2. Book an active room for tomorrow. Enter the organizer’s name and email, then save. Reuse the same email for later bookings.
3. Try the same room and time with another organizer: the backend rejects it.
4. Try the same organizer and time in another room: the backend rejects it.
5. Open Reservations. Use “Ready for check-in” to demonstrate check-in and early ending. It is available for 50 minutes after startup.
6. Use “Design review” to demonstrate ending an already checked-in meeting.
7. Open Usage. Conference Room 4 has an unattended sample booking; Conference Room 1 has a meeting that ended early.
8. Open Users to see active booking counts.

## Limitations

EF Core InMemory is required by the assignment and is not production persistence. Restarting clears edits. No accounts, room administration, user editing/deletion, sensor integration, or a weekly calendar are included. A daily timeline and browser-based Save as PDF report export are included. The app is intended for a local demonstration. The seeded examples and reporting rely on a correct server clock.

For a production deployment, use a relational database, database-level concurrency protection, authentication, and a supported deployment configuration. These are deliberately outside this assignment's scope.

## Verification performed

After the backend cleanup, the Release solution build succeeded with 0 warnings and 0 errors. All 49 existing tests passed (0 failed, 0 skipped). The frontend production build also passed after the redesign. A live HTTP smoke run passed 21 checks covering list/filter endpoints, create/edit, room and organizer conflicts, inactive rooms, capacity, cancellation, check-in, and early ending. Browser visual verification remains for manual review because no browser connection was available in the development session.

To repeat the live smoke check with the backend running, run `node scripts/smoke.mjs` from the root. It creates and cancels test bookings and may check in/end the seeded demonstration meeting. Restart the backend afterwards to restore the initial samples.

If this computer reports a certificate error during a fresh npm install, use Node's system certificate support for that terminal session (`$env:NODE_OPTIONS='--use-system-ca'`), then retry `npm.cmd install`. Do not disable certificate verification.

## Daily schedule and PDF export

Open Schedule, select a room and date, and see meetings in chronological order. Overnight meetings are included when they overlap the selected local day. Cancelled meetings remain visible with their status.

Open Usage, choose report dates, and click Save as PDF. In the browser print dialog, choose Save as PDF. The print stylesheet shows the report, date range, table and metric definitions, hiding navigation and controls. A4 landscape is the default print layout. This uses the browser rather than a server PDF library; print appearance has not been visually verified in this environment.

## GitHub submission

The source is ready to place in a GitHub repository. Dependency folders and build output are ignored. A remote repository has not been chosen or published automatically. Review the code and repository visibility before pushing.

