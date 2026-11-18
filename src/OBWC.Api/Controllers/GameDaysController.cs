namespace OBWC.Api.Controllers;

/// <summary>
/// A game day is one place and time that can host several games. The host owns the day.
/// Any member can post a game onto it. Only that game's owner, or an admin, can change the game.
/// </summary>
[ApiController]
[Route("api/game-days")]
public class GameDaysController(ScheduleService schedule, IAuthorizationService authorization) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GameDayDto>>> List(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        if (!TryWindow(year, month, out var from, out var to, out var error))
            return Bad(error!);
        var days = await schedule.ListGameDaysAsync(from, to, CurrentUserId, IsElevated, cancellationToken);
        return Ok(await schedule.MapGameDaysAsync(days, CurrentUserId, IsElevated, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<GameDayDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var day = await schedule.GetGameDayForReadAsync(id, CurrentUserId, IsElevated, cancellationToken);
        if (day is null)
            return NotFound();
        var mapped = await schedule.MapGameDaysAsync([day], CurrentUserId, IsElevated, cancellationToken);
        return Ok(mapped[0]);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateSeriesResultDto>> Create(CreateGameDayRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        if (!CanSeeMemberDetails)
            return Forbidden("Only members may schedule a game.");
        var (ok, created, firstId, error) = await schedule.CreateGameDaySeriesAsync(request, CurrentUserId, cancellationToken);
        if (!ok || firstId is null)
            return Bad(error ?? "Could not schedule the game day.");
        return CreatedAtAction(nameof(Get), new { id = firstId }, new CreateSeriesResultDto { Created = created, FirstSessionId = firstId });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<GameDayDto>> Update(Guid id, UpdateGameDayRequest request, CancellationToken cancellationToken)
    {
        var day = await schedule.GetGameDayAsync(id, cancellationToken);
        if (day is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, day);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.UpdateGameDayDetailsAsync(id, request, cancellationToken);
        if (!ok)
            return Bad(error ?? "Could not update the game day.");
        return await Get(id, cancellationToken);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Publish(Guid id, PublishSessionRequest request, CancellationToken cancellationToken)
    {
        var day = await schedule.GetGameDayAsync(id, cancellationToken);
        if (day is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, day);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.SetGameDayPublishedAsync(id, request.Published, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not change publishing.");
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Cancel(Guid id, CancelSessionRequest request, CancellationToken cancellationToken)
    {
        var day = await schedule.GetGameDayAsync(id, cancellationToken);
        if (day is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, day);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.CancelGameDayAsync(id, CurrentUserId!, request.Reason, request.EntireSeries, request.Notify, cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not cancel the game day.");
    }

    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleSessionRequest request, CancellationToken cancellationToken)
    {
        var day = await schedule.GetGameDayAsync(id, cancellationToken);
        if (day is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, day);
        if (deny is not null)
            return deny;
        var (ok, error) = await schedule.RescheduleGameDayAsync(
            id,
            request.NewStart,
            request.NewEnd,
            request.EntireSeriesFollowing,
            request.Notify,
            cancellationToken);
        return ok ? NoContent() : Bad(error ?? "Could not reschedule the game day.");
    }

    private static bool TryWindow(int? year, int? month, out DateTime from, out DateTime to, out string? error)
    {
        error = null;
        if (year is int y && month is int m)
        {
            if (y is < 2000 or > 2100 || m is < 1 or > 12)
            {
                from = default;
                to = default;
                error = "Choose a month between the years 2000 and 2100.";
                return false;
            }

            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var offset = ClubClock.Zone.GetUtcOffset(start);
            var fromOffset = new DateTimeOffset(start, offset);
            var toOffset = fromOffset.AddMonths(1).AddTicks(-1);
            from = fromOffset.UtcDateTime;
            to = toOffset.UtcDateTime;
            return true;
        }

        from = DateTime.UtcNow.AddDays(-14);
        to = DateTime.UtcNow.AddDays(120);
        return true;
    }
}
