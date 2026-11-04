namespace OBWC.Api.Controllers;

/// <summary>
/// Game schedule HTTP API. Edit, cancel, and reschedule require the event owner or an Admin/SuperAdmin.
/// A GM who did not create the event cannot change it.
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController(ScheduleService schedule, IAuthorizationService authorization) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CalendarEventDto>>> List(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        DateTimeOffset from;
        DateTimeOffset to;
        if (year is int y && month is int m)
        {
            if (y is < 2000 or > 2100 || m is < 1 or > 12)
                return Bad("Choose a month between the years 2000 and 2100.");
            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var offset = ClubClock.Zone.GetUtcOffset(start);
            from = new DateTimeOffset(start, offset);
            to = from.AddMonths(1).AddTicks(-1);
        }
        else
        {
            from = DateTimeOffset.UtcNow.AddDays(-14);
            to = DateTimeOffset.UtcNow.AddDays(120);
        }

        var sessions = await schedule.ListSessionsAsync(from.UtcDateTime, to.UtcDateTime, CurrentUserId, IsElevated, cancellationToken);
        return Ok(await schedule.MapManyAsync(sessions, CurrentUserId, IsElevated, cancellationToken));
    }

    [HttpGet("next")]
    [AllowAnonymous]
    public async Task<ActionResult<CalendarEventDto>> Next(CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.AddHours(-12);
        var sessions = await schedule.ListSessionsAsync(from, DateTime.UtcNow.AddDays(180), null, includeAllUnpublished: false, cancellationToken);
        var now = DateTime.UtcNow;
        var next = sessions.FirstOrDefault(session => !session.IsCancelled && (session.EndsAt ?? session.ScheduledAt) >= now);
        if (next is null)
            return NotFound();
        var mapped = await schedule.MapManyAsync([next], CurrentUserId, IsElevated, cancellationToken);
        return Ok(mapped[0]);
    }

    [HttpGet("mine")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<IReadOnlyList<CalendarEventDto>>> Mine(CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        return Ok(await schedule.ListMyScheduleAsync(CurrentUserId, IsElevated, cancellationToken));
    }

    [HttpGet("calendar.ics")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Ics(CancellationToken cancellationToken)
    {
        var sessions = await schedule.ListSessionsAsync(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(180),
            CurrentUserId,
            includeAllUnpublished: false,
            cancellationToken);
        var mapped = await schedule.MapManyAsync(sessions, CurrentUserId, IsElevated, cancellationToken);
        var ics = ScheduleService.BuildIcs("Omaha Beach Wargaming Club", "obwc", mapped);
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "obwc-calendar.ics");
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CalendarEventDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null || !CanSee(session))
            return NotFound();
        var mapped = await schedule.MapManyAsync([session], CurrentUserId, IsElevated, cancellationToken);
        return Ok(mapped[0]);
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<CreateSeriesResultDto>> Create(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        var (ok, created, firstId, error) = await schedule.CreateWeeklySeriesAsync(request, CurrentUserId, cancellationToken);
        if (!ok || firstId is null)
            return Bad(error ?? "Could not schedule the game.");
        return CreatedAtAction(nameof(Get), new { id = firstId }, new CreateSeriesResultDto { Created = created, FirstSessionId = firstId });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, UpdateSessionDetailsRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.UpdateSessionDetailsAsync(id, request, cancellationToken);
        if (!ok)
            return Bad(error ?? "Could not update the event.");
        return await Get(id, cancellationToken);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Publish(Guid id, PublishSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.SetPublishedAsync(id, request.Published, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not change publishing.");
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Cancel(Guid id, CancelSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.CancelEventAsync(id, CurrentUserId!, request.Reason, request.EntireSeries, request.Notify, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not cancel the event.");
    }

    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.RescheduleEventAsync(id, request.NewStart, request.NewEnd, request.EntireSeriesFollowing, request.Notify, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not reschedule the event.");
    }

    [HttpPut("{id:guid}/rsvp")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<RsvpResultDto>> Rsvp(Guid id, SetRsvpRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        var (ok, note, status) = await schedule.SetPlayerRsvpAsync(id, CurrentUserId, request.Status, cancellationToken);
        if (!ok)
            return Bad(note ?? "RSVP failed.");
        return Ok(new RsvpResultDto { Ok = true, Status = status, Note = note });
    }

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> CheckIn(Guid id, CheckInRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var target = string.IsNullOrWhiteSpace(request.UserId) ? CurrentUserId : request.UserId;
        if (target is null || CurrentUserId is null)
            return Unauthorized();
        var managingSomeoneElse = !string.Equals(target, CurrentUserId, StringComparison.Ordinal);
        if (managingSomeoneElse)
        {
            var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
            if (deny is not null)
                return deny;
        }

        var (ok, error) = await schedule.CheckInAsync(id, target, CurrentUserId, managingSomeoneElse, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Check-in failed.");
    }

    [HttpPost("{id:guid}/check-in/undo")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> UndoCheckIn(Guid id, CheckInRequest request, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();
        var target = string.IsNullOrWhiteSpace(request.UserId) ? CurrentUserId : request.UserId;
        if (target is null || CurrentUserId is null)
            return Unauthorized();
        var managingSomeoneElse = !string.Equals(target, CurrentUserId, StringComparison.Ordinal);
        if (managingSomeoneElse)
        {
            var deny = await DenyUnlessOwnerOrAdminAsync(authorization, session);
            if (deny is not null)
                return deny;
        }

        var (ok, error) = await schedule.UndoCheckInAsync(id, target, CurrentUserId, managingSomeoneElse, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not undo check-in.");
    }

    [HttpGet("{id:guid}/roster")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<IReadOnlyList<RosterRowDto>>> Roster(Guid id, CancellationToken cancellationToken)
    {
        var session = await schedule.GetSessionAsync(id, cancellationToken);
        if (session is null || !CanSee(session))
            return NotFound();
        return Ok(await schedule.GetRosterAsync(id, cancellationToken));
    }

    private bool CanSee(PlaySession session) =>
        session.IsPublished || IsElevated || CurrentUserId == session.CreatedByUserId;
}
