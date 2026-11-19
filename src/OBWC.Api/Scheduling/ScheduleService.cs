using System.Text;

namespace OBWC.Api.Scheduling;

/// <summary>
/// Club game calendar. Behavior follows the battletechomaha.com campaign calendar
/// (RSVP cap and waitlist, series reschedule, cancel, check-in, iCal) without campaigns,
/// contracts, awards, or the message center.
/// </summary>
public sealed class ScheduleService(ApplicationDbContext db, IClubMailer mail)
{
    public async Task<(bool Ok, PlaySession? Session, string? Error)> CreateSessionAsync(
        DateTimeOffset scheduledAt,
        DateTimeOffset? endsAt,
        string? title,
        string? notes,
        string createdByUserId,
        bool isPublished = true,
        bool allowPlayerRsvp = true,
        string? venue = null,
        string? address = null,
        string? onlineLink = null,
        string? locationNotes = null,
        int? playerCap = null,
        Guid? ruleSetId = null,
        string? customRuleSetName = null,
        Guid? gameDayId = null,
        string? hostName = null,
        Guid? venueId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return (false, null, "User required.");
        if (endsAt is DateTimeOffset end && end < scheduledAt)
            return (false, null, "End time must be after the start time.");
        if (playerCap is int cap && cap < 1)
            return (false, null, "Player cap must be at least 1 (or blank for unlimited).");
        var host = Clean(hostName);
        if (host is not null && host.Length > 200)
            return (false, null, "Host must be 200 characters or fewer.");
        var customName = Clean(customRuleSetName);
        if (customName is not null)
            ruleSetId = null;
        else if (ruleSetId is Guid ruleId && !await db.RuleSets.AnyAsync(ruleSet => ruleSet.Id == ruleId, cancellationToken))
            return (false, null, "That rule set is not on the club list.");

        string? storedVenue;
        string? storedAddress;
        Guid? storedVenueId;
        if (gameDayId is null)
        {
            var place = await ResolvePlaceAsync(venueId, venue, address, requirePlace: false, cancellationToken);
            if (!place.Ok)
                return (false, null, place.Error);
            storedVenueId = place.VenueId;
            storedVenue = place.Venue;
            storedAddress = place.Address;
        }
        else
        {
            // A game posted on a game day uses that day's place.
            storedVenueId = null;
            storedVenue = Clean(venue);
            storedAddress = Clean(address);
        }

        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            ScheduledAt = scheduledAt.UtcDateTime,
            EndsAt = endsAt?.UtcDateTime,
            Title = Clean(title),
            Notes = Clean(notes),
            Venue = storedVenue,
            Address = storedAddress,
            VenueId = storedVenueId,
            OnlineLink = Clean(onlineLink),
            LocationNotes = Clean(locationNotes),
            PlayerCap = playerCap,
            RuleSetId = ruleSetId,
            CustomRuleSetName = customName,
            GameDayId = gameDayId,
            HostName = host,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            IsPublished = isPublished,
            AllowPlayerRsvp = allowPlayerRsvp
        };

        db.PlaySessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return (true, session, null);
    }

    public async Task<(bool Ok, int Created, Guid? FirstSessionId, string? Error)> CreateWeeklySeriesAsync(
        CreateSessionRequest request,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var weeks = Math.Clamp(request.Weeks <= 0 ? 1 : request.Weeks, 1, 26);
        if (request.GameDayId is Guid gameDayId)
        {
            if (weeks > 1)
                return (false, 0, null, "Add a game to one game day at a time.");
            return await CreateGameOnDayAsync(request, gameDayId, createdByUserId, cancellationToken);
        }

        var (ruleOk, ruleSetId, customRuleSetName, ruleError) = await ResolveRuleSetAsync(request.RuleSetId, request.RuleSetName, cancellationToken);
        if (!ruleOk)
            return (false, 0, null, ruleError);
        // A single night is not a series. SeriesKey is set only when more than one week is created.
        Guid? seriesKey = weeks > 1 ? Guid.NewGuid() : null;
        var created = 0;
        Guid? firstId = null;
        string? lastError = null;

        for (var week = 0; week < weeks; week++)
        {
            var start = request.ScheduledAt.AddDays(7 * week);
            DateTimeOffset? end = request.EndsAt is DateTimeOffset ends ? ends.AddDays(7 * week) : null;
            var title = Clean(request.Title);
            if (title is not null && weeks > 1)
                title = $"{title} (week {week + 1})";

            var (ok, session, error) = await CreateSessionAsync(
                start, end, title, request.Notes, createdByUserId,
                request.IsPublished, request.AllowPlayerRsvp,
                request.Venue, request.Address, request.OnlineLink, request.LocationNotes,
                request.PlayerCap, ruleSetId, customRuleSetName, null, request.HostName, request.VenueId, cancellationToken);
            if (!ok || session is null)
            {
                lastError = error;
                continue;
            }

            if (seriesKey is Guid key)
            {
                var tracked = await db.PlaySessions.FirstAsync(item => item.Id == session.Id, cancellationToken);
                tracked.SeriesKey = key;
                await db.SaveChangesAsync(cancellationToken);
            }

            created++;
            firstId ??= session.Id;
        }

        return created == 0
            ? (false, 0, null, lastError ?? "Could not create the series.")
            : (true, created, firstId, lastError);
    }

    private async Task<(bool Ok, int Created, Guid? FirstSessionId, string? Error)> CreateGameOnDayAsync(
        CreateSessionRequest request,
        Guid gameDayId,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        var day = await db.GameDays.FirstOrDefaultAsync(item => item.Id == gameDayId, cancellationToken);
        if (day is null)
            return (false, 0, null, "That game day was not found.");
        if (day.IsCancelled)
            return (false, 0, null, "That game day was cancelled.");

        DateTimeOffset start;
        DateTimeOffset end;
        if (request.FollowGameDayTimes)
        {
            start = AsOffset(day.ScheduledAt);
            end = AsOffset(day.EndsAt);
        }
        else if (request.EndsAt is not DateTimeOffset requestedEnd)
        {
            return (false, 0, null, "A game on a game day needs a start and an end, or it uses the whole game day.");
        }
        else
        {
            start = request.ScheduledAt;
            end = requestedEnd;
            var outside = GameOutsideDay(start.UtcDateTime, end.UtcDateTime, day);
            if (outside is not null)
                return (false, 0, null, outside);
        }

        var (ruleOk, ruleSetId, customRuleSetName, ruleError) = await ResolveRuleSetAsync(request.RuleSetId, request.RuleSetName, cancellationToken);
        if (!ruleOk)
            return (false, 0, null, ruleError);

        var (ok, session, error) = await CreateSessionAsync(
            start, end, request.Title, request.Notes, createdByUserId,
            request.IsPublished, request.AllowPlayerRsvp,
            request.Venue, request.Address, request.OnlineLink, request.LocationNotes,
            request.PlayerCap, ruleSetId, customRuleSetName, day.Id, request.HostName, request.VenueId, cancellationToken);
        return ok && session is not null
            ? (true, 1, session.Id, null)
            : (false, 0, null, error ?? "Could not add the game.");
    }

    public async Task<List<PlaySession>> ListSessionsAsync(
        DateTime from,
        DateTime to,
        string? viewerUserId,
        bool includeAllUnpublished,
        CancellationToken cancellationToken = default)
    {
        var query = db.PlaySessions.AsNoTracking()
            .Include(session => session.Attendances)
            .Include(session => session.RuleSet)
            .Include(session => session.ListedVenue)
            .Include(session => session.GameDay!)
                .ThenInclude(day => day.ListedVenue)
            .Where(session => session.ScheduledAt >= from && session.ScheduledAt <= to);
        if (!includeAllUnpublished)
        {
            query = string.IsNullOrEmpty(viewerUserId)
                ? query.Where(session => session.IsPublished)
                : query.Where(session => session.IsPublished || session.CreatedByUserId == viewerUserId);
        }

        return await query.OrderBy(session => session.ScheduledAt).ThenBy(session => session.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<PlaySession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.PlaySessions
            .Include(session => session.Attendances)
            .Include(session => session.RuleSet)
            .Include(session => session.ListedVenue)
            .Include(session => session.GameDay!)
                .ThenInclude(day => day.ListedVenue)
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Note, SessionRsvpStatus Status)> SetPlayerRsvpAsync(
        Guid sessionId,
        string userId,
        SessionRsvpStatus rsvp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Sign in required.", SessionRsvpStatus.Unknown);
        if (rsvp == SessionRsvpStatus.Waitlist)
            rsvp = SessionRsvpStatus.Going;

        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.", SessionRsvpStatus.Unknown);
        if (session.IsCancelled)
            return (false, "This event was cancelled.", SessionRsvpStatus.Unknown);
        if (!session.IsPublished)
            return (false, "This event is not published yet.", SessionRsvpStatus.Unknown);
        if (!session.AllowPlayerRsvp)
            return (false, "RSVP is not enabled for this event.", SessionRsvpStatus.Unknown);
        if (!await IsMemberAsync(userId, cancellationToken))
            return (false, "Only members can RSVP.", SessionRsvpStatus.Unknown);

        var row = session.Attendances.FirstOrDefault(item => item.UserId == userId);
        if (row is null)
        {
            row = new SessionAttendance
            {
                Id = Guid.NewGuid(),
                PlaySessionId = session.Id,
                UserId = userId,
                Status = AttendanceStatus.Absent
            };
            // A preset Guid key added only through the collection is treated as an existing row.
            db.SessionAttendances.Add(row);
        }

        var wasGoingOrWait = row.RsvpStatus is SessionRsvpStatus.Going or SessionRsvpStatus.Waitlist;
        var final = rsvp;
        if (rsvp == SessionRsvpStatus.Going && session.PlayerCap is int cap && cap > 0)
        {
            var goingOthers = session.Attendances.Count(item => item.UserId != userId && item.RsvpStatus == SessionRsvpStatus.Going);
            if (goingOthers >= cap)
                final = SessionRsvpStatus.Waitlist;
        }

        row.RsvpStatus = final;
        row.RsvpAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (wasGoingOrWait && final is not (SessionRsvpStatus.Going or SessionRsvpStatus.Waitlist))
            await PromoteWaitlistAsync(session.Id, cancellationToken);

        var note = final == SessionRsvpStatus.Waitlist
            ? $"Event is full (cap {session.PlayerCap}). You are on the waitlist."
            : null;
        return (true, note, final);
    }

    public async Task<(bool Ok, string? Error)> UpdateSessionDetailsAsync(
        Guid sessionId,
        UpdateSessionDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PlayerCap is int cap && cap < 1)
            return (false, "Player cap must be at least 1 (or blank for unlimited).");
        var host = Clean(request.HostName);
        if (host is not null && host.Length > 200)
            return (false, "Host must be 200 characters or fewer.");
        var (ruleOk, ruleSetId, customRuleSetName, ruleError) = await ResolveRuleSetAsync(request.RuleSetId, request.RuleSetName, cancellationToken);
        if (!ruleOk)
            return (false, ruleError);

        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        if (session.IsCancelled)
            return (false, "Cancelled events cannot be edited. Schedule a new one instead.");

        session.Title = Clean(request.Title);
        session.Notes = Clean(request.Notes);
        if (session.GameDayId is null)
        {
            var place = await ResolvePlaceAsync(request.VenueId, request.Venue, request.Address, requirePlace: false, cancellationToken);
            if (!place.Ok)
                return (false, place.Error);
            session.VenueId = place.VenueId;
            session.Venue = place.Venue;
            session.Address = place.Address;
            session.OnlineLink = Clean(request.OnlineLink);
            session.LocationNotes = Clean(request.LocationNotes);
        }

        session.PlayerCap = request.PlayerCap;
        session.RuleSetId = ruleSetId;
        session.CustomRuleSetName = customRuleSetName;
        session.HostName = host;

        if (request.PlayerCap is int newCap && newCap > 0)
        {
            var going = session.Attendances
                .Where(row => row.RsvpStatus == SessionRsvpStatus.Going)
                .OrderBy(row => row.RsvpAt ?? DateTime.MaxValue)
                .ToList();
            foreach (var excess in going.Skip(newCap))
            {
                excess.RsvpStatus = SessionRsvpStatus.Waitlist;
                excess.RsvpAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await PromoteWaitlistAsync(session.Id, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CheckInAsync(
        Guid sessionId,
        string targetUserId,
        string actorUserId,
        bool asManager,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId) || string.IsNullOrWhiteSpace(actorUserId))
            return (false, "Sign in required.");
        if (!asManager && !string.Equals(targetUserId, actorUserId, StringComparison.Ordinal))
            return (false, "Players can only check themselves in.");

        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        if (session.IsCancelled)
            return (false, "This event was cancelled.");
        if (!session.IsPublished)
            return (false, "Event is not published.");

        var now = DateTime.UtcNow;
        var windowStart = session.ScheduledAt.AddHours(-3);
        var windowEnd = (session.EndsAt ?? session.ScheduledAt.AddHours(4)).AddHours(6);
        if (!asManager && (now < windowStart || now > windowEnd))
            return (false, "Check-in is only available near event time (from 3 hours before start).");

        var row = session.Attendances.FirstOrDefault(item => item.UserId == targetUserId);
        if (row is null)
        {
            row = new SessionAttendance
            {
                Id = Guid.NewGuid(),
                PlaySessionId = session.Id,
                UserId = targetUserId,
                RsvpStatus = SessionRsvpStatus.Unknown
            };
            // A preset Guid key added only through the collection is treated as an existing row.
            db.SessionAttendances.Add(row);
        }

        row.Status = AttendanceStatus.Present;
        row.CheckedInAt = now;
        row.CheckedInByUserId = actorUserId;
        row.MarkedAt = now;
        row.MarkedByUserId = actorUserId;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UndoCheckInAsync(
        Guid sessionId,
        string targetUserId,
        string actorUserId,
        bool asManager,
        CancellationToken cancellationToken = default)
    {
        if (!asManager && !string.Equals(targetUserId, actorUserId, StringComparison.Ordinal))
            return (false, "Players can only undo their own check-in.");

        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        var row = session.Attendances.FirstOrDefault(item => item.UserId == targetUserId);
        if (row is null)
            return (true, null);

        row.Status = AttendanceStatus.Absent;
        row.CheckedInAt = null;
        row.CheckedInByUserId = null;
        row.MarkedAt = DateTime.UtcNow;
        row.MarkedByUserId = actorUserId;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelEventAsync(
        Guid sessionId,
        string actorUserId,
        string? reason,
        bool entireSeries,
        bool notify,
        CancellationToken cancellationToken = default)
    {
        var root = await db.PlaySessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (root is null)
            return (false, "Event not found.");

        var targets = new List<PlaySession> { root };
        if (entireSeries && root.SeriesKey is Guid seriesKey)
        {
            targets = await db.PlaySessions
                .Where(item => item.SeriesKey == seriesKey && item.ScheduledAt >= root.ScheduledAt)
                .ToListAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        foreach (var session in targets)
        {
            session.IsCancelled = true;
            session.CancelledUtc = now;
            session.CancelledByUserId = actorUserId;
            session.CancellationReason = Clean(reason);
            session.AllowPlayerRsvp = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (notify)
        {
            await NotifyRsvpPlayersAsync(targets.Select(item => item.Id).ToList(),
                "Cancelled: club game",
                "This event was cancelled.",
                reason,
                cancellationToken);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RescheduleEventAsync(
        Guid sessionId,
        DateTimeOffset newStart,
        DateTimeOffset? newEnd,
        bool entireSeriesFollowing,
        bool notify,
        CancellationToken cancellationToken = default)
    {
        if (newEnd is DateTimeOffset end && end < newStart)
            return (false, "End time must be after start.");

        var root = await db.PlaySessions.Include(item => item.GameDay)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (root is null)
            return (false, "Event not found.");
        if (root.IsCancelled)
            return (false, "Cancelled events stay cancelled. Schedule a new one.");

        var oldStart = DateTime.SpecifyKind(root.ScheduledAt, DateTimeKind.Utc);
        var delta = newStart.UtcDateTime - oldStart;
        var targets = new List<PlaySession> { root };
        if (entireSeriesFollowing && root.SeriesKey is Guid seriesKey)
        {
            var rest = await db.PlaySessions.Include(item => item.GameDay)
                .Where(item => item.SeriesKey == seriesKey && item.Id != root.Id && item.ScheduledAt >= oldStart && !item.IsCancelled)
                .ToListAsync(cancellationToken);
            targets.AddRange(rest);
        }

        var proposed = new List<(PlaySession Session, DateTime Start, DateTime? End)>();
        foreach (var session in targets)
        {
            var startUtc = session.Id == root.Id ? newStart.UtcDateTime : session.ScheduledAt.Add(delta);
            DateTime? endUtc = session.Id == root.Id
                ? newEnd?.UtcDateTime
                : session.EndsAt is DateTime existingEnd ? existingEnd.Add(delta) : null;
            if (session.GameDay is not null)
            {
                if (endUtc is null)
                    return (false, "A game on a game day needs a start and an end inside the game day.");
                var outside = GameOutsideDay(startUtc, endUtc.Value, session.GameDay);
                if (outside is not null)
                    return (false, outside);
            }

            proposed.Add((session, startUtc, endUtc));
        }

        foreach (var (session, startUtc, endUtc) in proposed)
        {
            session.ScheduledAt = startUtc;
            session.EndsAt = endUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (notify)
        {
            await NotifyRsvpPlayersAsync(targets.Select(item => item.Id).ToList(),
                "Rescheduled: club game",
                $"Event moved to {ClubTime.FormatDateTime(newStart)}. Following series weeks move by the same amount when that option is selected.",
                null,
                cancellationToken);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetPublishedAsync(Guid sessionId, bool published, CancellationToken cancellationToken = default)
    {
        var session = await db.PlaySessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        session.IsPublished = published;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<List<RosterRowDto>> GetRosterAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.PlaySessions.AsNoTracking()
            .Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return [];

        var ids = session.Attendances.Select(row => row.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        return session.Attendances
            .Where(row => row.RsvpStatus is SessionRsvpStatus.Going or SessionRsvpStatus.Maybe or SessionRsvpStatus.Waitlist
                || row.Status == AttendanceStatus.Present)
            .Select(row =>
            {
                users.TryGetValue(row.UserId, out var user);
                return new RosterRowDto
                {
                    UserId = row.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(user?.DisplayName) ? "Member" : user!.DisplayName,
                    Email = user?.Email ?? "",
                    Rsvp = row.RsvpStatus,
                    RsvpAt = row.RsvpAt is DateTime rsvpAt ? AsOffset(rsvpAt) : null,
                    NightStatus = row.Status,
                    CheckedInAt = row.CheckedInAt is DateTime checkedIn ? AsOffset(checkedIn) : null
                };
            })
            .OrderBy(row => RsvpSort(row.Rsvp))
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<CalendarEventDto>> ListMyScheduleAsync(
        string userId,
        bool viewerIsAdmin,
        CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.AddDays(-45);
        var to = DateTime.UtcNow.AddDays(120);
        var sessions = await ListSessionsAsync(from, to, userId, includeAllUnpublished: false, cancellationToken);
        var mapped = await MapManyAsync(sessions, userId, viewerIsAdmin, cancellationToken);
        return mapped
            .Where(item => item.MyRsvp is SessionRsvpStatus.Going or SessionRsvpStatus.Maybe or SessionRsvpStatus.Waitlist || item.IAmCheckedIn)
            .OrderBy(item => item.ScheduledAt)
            .ToList();
    }

    public Task<int> SeedMemberOnUpcomingSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        // A member is listed only after they answer or are checked in. Registration does not add a row.
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(0);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> MapManyAsync(
        IReadOnlyList<PlaySession> sessions,
        string? viewerUserId,
        bool viewerIsAdmin,
        CancellationToken cancellationToken)
    {
        var ownerIds = sessions.Select(session => session.CreatedByUserId).Distinct().ToList();
        var gmRoleId = await db.Roles.Where(role => role.Name == RoleNames.Gm).Select(role => role.Id).FirstOrDefaultAsync(cancellationToken);
        var gmIds = string.IsNullOrEmpty(gmRoleId)
            ? []
            : await db.UserRoles.Where(link => link.RoleId == gmRoleId && ownerIds.Contains(link.UserId)).Select(link => link.UserId).ToListAsync(cancellationToken);
        var gmSet = gmIds.ToHashSet(StringComparer.Ordinal);
        var names = await db.Users.AsNoTracking()
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var viewerIsGm = !string.IsNullOrEmpty(viewerUserId)
            && !string.IsNullOrEmpty(gmRoleId)
            && await db.UserRoles.AnyAsync(link => link.RoleId == gmRoleId && link.UserId == viewerUserId, cancellationToken);
        var sessionIds = sessions.Select(session => session.Id).ToList();
        var photos = sessionIds.Count == 0
            ? []
            : await db.EventPhotos.AsNoTracking()
                .Where(photo => sessionIds.Contains(photo.PlaySessionId))
                .OrderBy(photo => photo.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        var photosBySession = photos.GroupBy(photo => photo.PlaySessionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<EventPhoto>)group.ToList());
        return sessions.Select(session => Map(
            session,
            viewerUserId,
            viewerIsAdmin,
            viewerIsGm,
            gmSet,
            names,
            photosBySession.GetValueOrDefault(session.Id) ?? [])).ToList();
    }

    public static string BuildIcs(string calendarName, string slug, IReadOnlyList<CalendarEventDto> events)
    {
        static string Esc(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace(";", "\\;", StringComparison.Ordinal)
                .Replace(",", "\\,", StringComparison.Ordinal)
                .Replace("\r\n", "\\n", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        static string UtcStamp(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//OBWC//Calendar//EN");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine("METHOD:PUBLISH");
        builder.AppendLine($"X-WR-CALNAME:{Esc(calendarName)} (calendar)");

        foreach (var item in events.Where(item => item.IsPublished && !item.IsCancelled))
        {
            var end = item.EndsAt ?? item.ScheduledAt.AddHours(4);
            var summary = string.IsNullOrWhiteSpace(item.Title) ? "Club game" : item.Title!;
            if (item.ScheduleKind == "GameDay" && !summary.StartsWith("Game day", StringComparison.OrdinalIgnoreCase))
                summary = "Game day: " + summary;
            var description = item.Notes ?? "";
            if (!string.IsNullOrWhiteSpace(item.HostName))
                description = string.IsNullOrEmpty(description) ? "Host: " + item.HostName : description + "\\nHost: " + item.HostName;
            if (!string.IsNullOrWhiteSpace(item.LocationNotes))
                description = string.IsNullOrEmpty(description) ? item.LocationNotes! : description + "\\n" + item.LocationNotes;
            if (!string.IsNullOrWhiteSpace(item.OnlineLink))
                description = string.IsNullOrEmpty(description) ? "Online: " + item.OnlineLink : description + "\\nOnline: " + item.OnlineLink;
            var location = string.Join(", ", new[] { item.Venue, item.Address }.Where(part => !string.IsNullOrWhiteSpace(part)));

            builder.AppendLine("BEGIN:VEVENT");
            builder.AppendLine($"UID:{item.Id:N}@obwc-{Esc(slug)}");
            builder.AppendLine($"DTSTAMP:{UtcStamp(DateTimeOffset.UtcNow)}");
            builder.AppendLine($"DTSTART:{UtcStamp(item.ScheduledAt)}");
            builder.AppendLine($"DTEND:{UtcStamp(end)}");
            builder.AppendLine($"SUMMARY:{Esc(summary)}");
            if (!string.IsNullOrWhiteSpace(description))
                builder.AppendLine($"DESCRIPTION:{Esc(description)}");
            if (!string.IsNullOrWhiteSpace(location))
                builder.AppendLine($"LOCATION:{Esc(location)}");
            builder.AppendLine("URL:schedule");
            builder.AppendLine("END:VEVENT");
        }

        builder.AppendLine("END:VCALENDAR");
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    public async Task<(bool Ok, int Created, Guid? FirstId, string? Error)> CreateGameDaySeriesAsync(
        CreateGameDayRequest request,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return (false, 0, null, "User required.");
        if (request.EndsAt is not DateTimeOffset endsAt)
            return (false, 0, null, "A game day needs a start and an end.");
        if (endsAt <= request.ScheduledAt)
            return (false, 0, null, "End time must be after the start time.");
        var place = await ResolvePlaceAsync(request.VenueId, request.Venue, request.Address, requirePlace: true, cancellationToken);
        if (!place.Ok)
            return (false, 0, null, place.Error);

        var weeks = Math.Clamp(request.Weeks <= 0 ? 1 : request.Weeks, 1, 26);
        Guid? seriesKey = weeks > 1 ? Guid.NewGuid() : null;
        var created = 0;
        Guid? firstId = null;
        for (var week = 0; week < weeks; week++)
        {
            var title = Clean(request.Title);
            if (title is not null && weeks > 1)
                title = $"{title} (week {week + 1})";
            var day = new GameDay
            {
                Id = Guid.NewGuid(),
                ScheduledAt = request.ScheduledAt.AddDays(7 * week).UtcDateTime,
                EndsAt = endsAt.AddDays(7 * week).UtcDateTime,
                Title = title,
                Notes = Clean(request.Notes),
                VenueId = place.VenueId,
                Venue = place.Venue,
                Address = place.Address,
                OnlineLink = Clean(request.OnlineLink),
                LocationNotes = Clean(request.LocationNotes),
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                IsPublished = request.IsPublished,
                SeriesKey = seriesKey
            };
            db.GameDays.Add(day);
            created++;
            firstId ??= day.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (true, created, firstId, null);
    }

    public async Task<List<GameDay>> ListGameDaysAsync(
        DateTime from,
        DateTime to,
        string? viewerUserId,
        bool includeAllUnpublished,
        CancellationToken cancellationToken = default)
    {
        var query = db.GameDays.AsNoTracking()
            .Include(day => day.ListedVenue)
            .Include(day => day.Games).ThenInclude(game => game.Attendances)
            .Include(day => day.Games).ThenInclude(game => game.RuleSet)
            .Where(day => day.ScheduledAt <= to && day.EndsAt >= from);
        if (!includeAllUnpublished)
        {
            query = string.IsNullOrEmpty(viewerUserId)
                ? query.Where(day => day.IsPublished)
                : query.Where(day => day.IsPublished
                    || day.CreatedByUserId == viewerUserId
                    || day.Games.Any(game => game.CreatedByUserId == viewerUserId));
        }

        var days = await query.OrderBy(day => day.ScheduledAt).ThenBy(day => day.CreatedAt).ToListAsync(cancellationToken);
        foreach (var day in days)
            ApplyGameVisibility(day, viewerUserId, includeAllUnpublished);
        return days;
    }

    public Task<GameDay?> GetGameDayAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.GameDays.FirstOrDefaultAsync(day => day.Id == id, cancellationToken);

    public async Task<GameDay?> GetGameDayForReadAsync(
        Guid id,
        string? viewerUserId,
        bool includeAllUnpublished,
        CancellationToken cancellationToken = default)
    {
        var day = await db.GameDays.AsNoTracking()
            .Include(item => item.ListedVenue)
            .Include(item => item.Games).ThenInclude(game => game.Attendances)
            .Include(item => item.Games).ThenInclude(game => game.RuleSet)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (day is null)
            return null;
        var canSee = includeAllUnpublished
            || day.IsPublished
            || day.CreatedByUserId == viewerUserId
            || day.Games.Any(game => game.CreatedByUserId == viewerUserId);
        if (!canSee)
            return null;
        ApplyGameVisibility(day, viewerUserId, includeAllUnpublished);
        return day;
    }

    public async Task<(bool Ok, string? Error)> UpdateGameDayDetailsAsync(
        Guid id,
        UpdateGameDayRequest request,
        CancellationToken cancellationToken = default)
    {
        var place = await ResolvePlaceAsync(request.VenueId, request.Venue, request.Address, requirePlace: true, cancellationToken);
        if (!place.Ok)
            return (false, place.Error);
        var day = await db.GameDays.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (day is null)
            return (false, "That game day was not found.");
        if (day.IsCancelled)
            return (false, "Cancelled game days cannot be edited. Schedule a new one instead.");

        day.Title = Clean(request.Title);
        day.Notes = Clean(request.Notes);
        day.VenueId = place.VenueId;
        day.Venue = place.Venue;
        day.Address = place.Address;
        day.OnlineLink = Clean(request.OnlineLink);
        day.LocationNotes = Clean(request.LocationNotes);
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetGameDayPublishedAsync(Guid id, bool published, CancellationToken cancellationToken = default)
    {
        var day = await db.GameDays.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (day is null)
            return (false, "That game day was not found.");
        day.IsPublished = published;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelGameDayAsync(
        Guid id,
        string actorUserId,
        string? reason,
        bool entireSeries,
        bool notify,
        CancellationToken cancellationToken = default)
    {
        var root = await db.GameDays.Include(day => day.Games).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (root is null)
            return (false, "That game day was not found.");

        var targets = new List<GameDay> { root };
        if (entireSeries && root.SeriesKey is Guid seriesKey)
        {
            var rest = await db.GameDays.Include(day => day.Games)
                .Where(item => item.SeriesKey == seriesKey && item.Id != root.Id && item.ScheduledAt >= root.ScheduledAt)
                .ToListAsync(cancellationToken);
            targets.AddRange(rest);
        }

        var now = DateTime.UtcNow;
        var gameIds = new List<Guid>();
        foreach (var day in targets)
        {
            day.IsCancelled = true;
            day.CancelledUtc = now;
            day.CancelledByUserId = actorUserId;
            day.CancellationReason = Clean(reason);
            foreach (var game in day.Games.Where(game => !game.IsCancelled))
            {
                game.IsCancelled = true;
                game.CancelledUtc = now;
                game.CancelledByUserId = actorUserId;
                game.CancellationReason = Clean(reason);
                game.AllowPlayerRsvp = false;
                gameIds.Add(game.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (notify && gameIds.Count > 0)
        {
            await NotifyRsvpPlayersAsync(
                gameIds,
                "Cancelled: game day",
                "This game day was cancelled, and the games on it were cancelled with it.",
                reason,
                cancellationToken);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RescheduleGameDayAsync(
        Guid id,
        DateTimeOffset newStart,
        DateTimeOffset? newEnd,
        bool entireSeriesFollowing,
        bool notify,
        CancellationToken cancellationToken = default)
    {
        if (newEnd is not DateTimeOffset end)
            return (false, "A game day needs a start and an end.");
        if (end <= newStart)
            return (false, "End time must be after the start time.");

        var root = await db.GameDays.Include(day => day.Games).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (root is null)
            return (false, "That game day was not found.");
        if (root.IsCancelled)
            return (false, "Cancelled game days stay cancelled. Schedule a new one.");

        var oldStart = DateTime.SpecifyKind(root.ScheduledAt, DateTimeKind.Utc);
        var delta = newStart.UtcDateTime - oldStart;
        var targets = new List<GameDay> { root };
        if (entireSeriesFollowing && root.SeriesKey is Guid seriesKey)
        {
            var rest = await db.GameDays.Include(day => day.Games)
                .Where(item => item.SeriesKey == seriesKey && item.Id != root.Id && item.ScheduledAt >= oldStart && !item.IsCancelled)
                .ToListAsync(cancellationToken);
            targets.AddRange(rest);
        }

        var proposedDays = new List<(GameDay Day, DateTime Start, DateTime End)>();
        var proposedGames = new List<(PlaySession Game, DateTime Start, DateTime End)>();
        foreach (var day in targets)
        {
            var startUtc = day.Id == root.Id ? newStart.UtcDateTime : day.ScheduledAt.Add(delta);
            var endUtc = day.Id == root.Id ? end.UtcDateTime : day.EndsAt.Add(delta);
            if (endUtc <= startUtc)
                return (false, "End time must be after the start time.");
            foreach (var game in day.Games)
            {
                if (game.EndsAt is not DateTime existingEnd)
                    return (false, "A game would fall outside the game day. Lengthen the day or detach that game first.");
                var gameStart = game.ScheduledAt.Add(delta);
                var gameEnd = existingEnd.Add(delta);
                if (gameStart < startUtc || gameEnd > endUtc || gameEnd < gameStart)
                    return (false, "A game would fall outside the game day. Lengthen the day or detach that game first.");
                proposedGames.Add((game, gameStart, gameEnd));
            }

            proposedDays.Add((day, startUtc, endUtc));
        }

        foreach (var (day, startUtc, endUtc) in proposedDays)
        {
            day.ScheduledAt = startUtc;
            day.EndsAt = endUtc;
        }

        foreach (var (game, startUtc, endUtc) in proposedGames)
        {
            game.ScheduledAt = startUtc;
            game.EndsAt = endUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (notify && proposedGames.Count > 0)
        {
            await NotifyRsvpPlayersAsync(
                proposedGames.Select(item => item.Game.Id).ToList(),
                "Rescheduled: game day",
                $"Game day moved to {ClubTime.FormatDateTime(newStart)}. Games on it move by the same amount.",
                null,
                cancellationToken);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DetachGameAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.PlaySessions.Include(item => item.GameDay)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        if (session.GameDay is null)
            return (false, "This game is not on a game day.");

        var day = session.GameDay;
        session.VenueId ??= day.VenueId;
        session.Venue = Clean(session.Venue) ?? day.Venue;
        session.Address = Clean(session.Address) ?? day.Address;
        session.OnlineLink = Clean(session.OnlineLink) ?? day.OnlineLink;
        session.LocationNotes = Clean(session.LocationNotes) ?? day.LocationNotes;
        session.GameDayId = null;
        session.GameDay = null;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<CalendarEventDto?> NextCardAsync(string? viewerUserId, bool viewerIsAdmin, CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.AddHours(-12);
        var to = DateTime.UtcNow.AddDays(180);
        var now = DateTime.UtcNow;
        var sessions = await ListSessionsAsync(from, to, null, includeAllUnpublished: false, cancellationToken);
        var days = await ListGameDaysAsync(from, to, null, includeAllUnpublished: false, cancellationToken);
        var nextGame = sessions
            .Where(session => session.GameDayId is null && !session.IsCancelled && (session.EndsAt ?? session.ScheduledAt) >= now)
            .OrderBy(session => session.ScheduledAt)
            .FirstOrDefault();
        var nextDay = days
            .Where(day => !day.IsCancelled && day.EndsAt >= now)
            .OrderBy(day => day.ScheduledAt)
            .FirstOrDefault();
        if (nextDay is not null && (nextGame is null || nextDay.ScheduledAt <= nextGame.ScheduledAt))
        {
            var mapped = await MapGameDaysAsync([nextDay], viewerUserId, viewerIsAdmin, cancellationToken);
            return ToCalendarEvent(mapped[0]);
        }

        if (nextGame is null)
            return null;
        var games = await MapManyAsync([nextGame], viewerUserId, viewerIsAdmin, cancellationToken);
        return games[0];
    }

    public async Task<IReadOnlyList<GameDayDto>> MapGameDaysAsync(
        IReadOnlyList<GameDay> days,
        string? viewerUserId,
        bool viewerIsAdmin,
        CancellationToken cancellationToken)
    {
        foreach (var day in days)
        {
            foreach (var game in day.Games)
                game.GameDay ??= day;
        }

        var mappedGames = await MapManyAsync(days.SelectMany(day => day.Games).ToList(), viewerUserId, viewerIsAdmin, cancellationToken);
        var byId = mappedGames.ToDictionary(game => game.Id);
        var ownerIds = days.Select(day => day.CreatedByUserId).Distinct().ToList();
        var names = ownerIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.AsNoTracking()
                .Where(user => ownerIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return days.Select(day =>
        {
            names.TryGetValue(day.CreatedByUserId, out var ownerName);
            var games = day.Games.Select(game => byId[game.Id]).OrderBy(game => game.ScheduledAt).ToList();
            var going = day.Games
                .Where(game => !game.IsCancelled)
                .SelectMany(game => game.Attendances)
                .Where(row => row.RsvpStatus == SessionRsvpStatus.Going)
                .Select(row => row.UserId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            var place = ShownPlace(day.VenueId, day.ListedVenue, day.Venue, day.Address);
            return new GameDayDto
            {
                Id = day.Id,
                ScheduledAt = AsOffset(day.ScheduledAt),
                EndsAt = AsOffset(day.EndsAt),
                Title = day.Title,
                Notes = day.Notes,
                Venue = place.Name,
                Address = place.Address,
                VenueId = place.Id,
                OnlineLink = day.OnlineLink,
                LocationNotes = day.LocationNotes,
                IsPublished = day.IsPublished,
                IsCancelled = day.IsCancelled,
                CancellationReason = day.CancellationReason,
                SeriesKey = day.SeriesKey,
                OwnerUserId = day.CreatedByUserId,
                OwnerDisplayName = string.IsNullOrWhiteSpace(ownerName) ? "Member" : ownerName,
                CanEdit = viewerIsAdmin || (!string.IsNullOrEmpty(viewerUserId) && viewerUserId == day.CreatedByUserId),
                PlayersGoing = going,
                LocationDisplay = FormatLocation(place.Name, place.Address, day.LocationNotes),
                Games = games
            };
        }).ToList();
    }

    public static CalendarEventDto ToCalendarEvent(GameDayDto day)
    {
        var titles = day.Games
            .Where(game => !game.IsCancelled)
            .Select(game => string.IsNullOrWhiteSpace(game.Title) ? "Club game" : game.Title!)
            .ToList();
        return new CalendarEventDto
        {
            Id = day.Id,
            ScheduledAt = day.ScheduledAt,
            EndsAt = day.EndsAt,
            Title = string.IsNullOrWhiteSpace(day.Title) ? "Game day" : day.Title,
            Notes = day.Notes,
            IsPublished = day.IsPublished,
            AllowPlayerRsvp = false,
            Going = day.PlayersGoing,
            IsCancelled = day.IsCancelled,
            CancellationReason = day.CancellationReason,
            SeriesKey = day.SeriesKey,
            Venue = day.Venue,
            Address = day.Address,
            VenueId = day.VenueId,
            OnlineLink = day.OnlineLink,
            LocationNotes = day.LocationNotes,
            RuleSetName = titles.Count == 0 ? "No games posted yet" : string.Join(", ", titles),
            ScheduleKind = "GameDay",
            OwnerUserId = day.OwnerUserId,
            OwnerDisplayName = day.OwnerDisplayName,
            CanEdit = day.CanEdit,
            LocationDisplay = day.LocationDisplay
        };
    }

    public static void ApplyGameVisibility(GameDay day, string? viewerUserId, bool includeAllUnpublished)
    {
        day.Games = day.Games
            .Where(game => includeAllUnpublished
                || game.IsPublished
                || game.CreatedByUserId == viewerUserId
                || day.CreatedByUserId == viewerUserId)
            .OrderBy(game => game.ScheduledAt)
            .ThenBy(game => game.CreatedAt)
            .ToList();
        foreach (var game in day.Games)
            game.GameDay ??= day;
    }

    private async Task PromoteWaitlistAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null || session.PlayerCap is not int cap || cap < 1)
            return;

        var slots = cap - session.Attendances.Count(row => row.RsvpStatus == SessionRsvpStatus.Going);
        if (slots <= 0)
            return;

        var waiting = session.Attendances
            .Where(row => row.RsvpStatus == SessionRsvpStatus.Waitlist)
            .OrderBy(row => row.RsvpAt ?? DateTime.MaxValue)
            .Take(slots)
            .ToList();
        if (waiting.Count == 0)
            return;

        foreach (var row in waiting)
        {
            row.RsvpStatus = SessionRsvpStatus.Going;
            row.RsvpAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var title = string.IsNullOrWhiteSpace(session.Title) ? "Club game" : session.Title!;
        var when = ClubTime.FormatDateTime(AsOffset(session.ScheduledAt));
        var ids = waiting.Select(row => row.UserId).ToList();
        var emails = await db.Users.AsNoTracking().Where(user => ids.Contains(user.Id) && user.Email != null).Select(user => user.Email!).ToListAsync(cancellationToken);
        foreach (var email in emails)
        {
            await TrySendAsync(email, $"Waitlist: you're Going — {title}",
                $"A seat opened for \"{title}\" on {when}.\n\nYou have been moved from the waitlist to Going.\n\nSee the game schedule for details.",
                cancellationToken);
        }
    }

    private async Task NotifyRsvpPlayersAsync(
        IReadOnlyList<Guid> sessionIds,
        string subject,
        string lead,
        string? reason,
        CancellationToken cancellationToken)
    {
        var sessions = await db.PlaySessions.AsNoTracking().Include(item => item.Attendances)
            .Where(item => sessionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var userIds = sessions.SelectMany(item => item.Attendances)
            .Where(row => row.RsvpStatus is SessionRsvpStatus.Going or SessionRsvpStatus.Maybe or SessionRsvpStatus.Waitlist)
            .Select(row => row.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var emails = await db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.Email != null)
            .Select(user => user.Email!)
            .ToListAsync(cancellationToken);
        var body = new StringBuilder();
        body.AppendLine(lead);
        if (!string.IsNullOrWhiteSpace(reason))
            body.AppendLine($"Reason: {reason.Trim()}");
        body.AppendLine();
        foreach (var session in sessions.OrderBy(item => item.ScheduledAt))
        {
            var title = string.IsNullOrWhiteSpace(session.Title) ? "Club game" : session.Title!;
            body.AppendLine($"• {ClubTime.FormatDateTime(AsOffset(session.ScheduledAt))} — {title}");
        }

        body.AppendLine();
        body.AppendLine("See the game schedule for details.");
        foreach (var email in emails)
            await TrySendAsync(email, subject, body.ToString(), cancellationToken);
    }

    private async Task TrySendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            await mail.SendAsync(email, subject, body, null, null, cancellationToken);
        }
        catch (MailNotConfiguredException)
        {
        }
    }

    private async Task<bool> IsMemberAsync(string userId, CancellationToken cancellationToken)
    {
        var roleIds = await db.Roles
            .Where(role => role.Name == RoleNames.Member || role.Name == RoleNames.Admin || role.Name == RoleNames.SuperAdmin)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        return await db.UserRoles.AnyAsync(link => link.UserId == userId && roleIds.Contains(link.RoleId), cancellationToken);
    }

    private static CalendarEventDto Map(
        PlaySession session,
        string? viewerUserId,
        bool viewerIsAdmin,
        bool viewerIsGm,
        HashSet<string> gmIds,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyList<EventPhoto> photos)
    {
        var rows = session.Attendances;
        var mine = string.IsNullOrEmpty(viewerUserId) ? null : rows.FirstOrDefault(row => row.UserId == viewerUserId);
        var noShows = session.ScheduledAt < DateTime.UtcNow.AddHours(-1)
            ? rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Going && row.Status != AttendanceStatus.Present)
            : 0;
        var day = session.GameDay;
        var place = day is null
            ? ShownPlace(session.VenueId, session.ListedVenue, session.Venue, session.Address)
            : ShownPlace(day.VenueId, day.ListedVenue, day.Venue, day.Address);
        var venue = place.Name;
        var address = place.Address;
        var online = day is null ? session.OnlineLink : day.OnlineLink;
        var locationNotes = day is null ? session.LocationNotes : day.LocationNotes;
        names.TryGetValue(session.CreatedByUserId, out var ownerName);
        return new CalendarEventDto
        {
            Id = session.Id,
            ScheduledAt = AsOffset(session.ScheduledAt),
            EndsAt = session.EndsAt is DateTime endsAt ? AsOffset(endsAt) : null,
            Title = session.Title,
            Notes = session.Notes,
            IsPublished = session.IsPublished,
            AllowPlayerRsvp = session.AllowPlayerRsvp,
            Going = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Going),
            Maybe = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Maybe),
            NotGoing = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.NotGoing),
            Waitlist = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Waitlist),
            MyRsvp = mine?.RsvpStatus ?? SessionRsvpStatus.Unknown,
            Present = rows.Count(row => row.Status == AttendanceStatus.Present),
            IAmCheckedIn = mine?.Status == AttendanceStatus.Present,
            NoShows = noShows,
            SeriesKey = session.SeriesKey,
            Venue = venue,
            Address = address,
            VenueId = place.Id,
            OnlineLink = online,
            LocationNotes = locationNotes,
            PlayerCap = session.PlayerCap,
            IsCancelled = session.IsCancelled,
            CancellationReason = session.CancellationReason,
            RuleSetId = session.RuleSetId,
            RuleSetName = session.RuleSet?.Name ?? session.CustomRuleSetName,
            GameDayId = session.GameDayId,
            CanDetach = session.GameDayId is not null
                && (viewerIsAdmin || (!string.IsNullOrEmpty(viewerUserId) && viewerUserId == day?.CreatedByUserId)),
            ScheduleKind = "Game",
            OwnerUserId = session.CreatedByUserId,
            OwnerDisplayName = string.IsNullOrWhiteSpace(ownerName) ? "Member" : ownerName,
            OwnerIsGm = gmIds.Contains(session.CreatedByUserId),
            HostName = session.HostName,
            CanEdit = viewerIsAdmin || (!string.IsNullOrEmpty(viewerUserId) && viewerUserId == session.CreatedByUserId),
            CanAddPhotos = viewerIsAdmin || (viewerIsGm && !string.IsNullOrEmpty(viewerUserId) && viewerUserId == session.CreatedByUserId),
            Photos = photos.Select(photo => new EventPhotoDto
            {
                Id = photo.Id,
                Caption = photo.Caption,
                Url = $"api/files/event-photos/{photo.Id}",
                CreatedAtUtc = DateTime.SpecifyKind(photo.CreatedAtUtc, DateTimeKind.Utc),
                CanEdit = viewerIsAdmin || (!string.IsNullOrEmpty(viewerUserId) && viewerUserId == photo.OwnerUserId)
            }).ToArray(),
            LocationDisplay = FormatLocation(venue, address, locationNotes)
        };
    }

    private static int RsvpSort(SessionRsvpStatus status) => status switch
    {
        SessionRsvpStatus.Going => 0,
        SessionRsvpStatus.Waitlist => 1,
        SessionRsvpStatus.Maybe => 2,
        SessionRsvpStatus.NotGoing => 3,
        _ => 4
    };

    private static DateTimeOffset AsOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    /// <summary>
    /// A listed id wins. A typed name that matches one club venue uses that venue and copies its address.
    /// Any other name is stored as written and is not added to the club list. The stored name fits the schedule column.
    /// </summary>
    private async Task<(bool Ok, Guid? VenueId, string? Venue, string? Address, string? Error)> ResolvePlaceAsync(
        Guid? venueId,
        string? venue,
        string? address,
        bool requirePlace,
        CancellationToken cancellationToken)
    {
        if (venueId is Guid id)
        {
            var listed = await db.Venues.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (listed is null)
                return (false, null, null, null, "That venue is not on the club list.");
            return (true, listed.Id, FitVenueName(listed.Name), Clean(listed.Address), null);
        }

        var typed = Clean(venue);
        if (typed is not null)
        {
            var matches = await db.Venues.AsNoTracking()
                .Where(item => item.Name.ToLower() == typed.ToLower())
                .OrderBy(item => item.Id)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (matches.Count == 1)
            {
                var listed = matches[0];
                return (true, listed.Id, FitVenueName(listed.Name), Clean(listed.Address), null);
            }
        }

        var storedVenue = FitVenueName(typed);
        var storedAddress = Clean(address);
        if (requirePlace && storedVenue is null && storedAddress is null)
            return (false, null, null, null, "A game day needs a venue or an address.");
        return (true, null, storedVenue, storedAddress, null);
    }

    private static (Guid? Id, string? Name, string? Address) ShownPlace(Guid? venueId, Venue? listed, string? name, string? address) =>
        listed is null ? (venueId, name, address) : (listed.Id, listed.Name, listed.Address);

    private static string? FitVenueName(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null || cleaned.Length <= 160)
            return cleaned;
        return cleaned[..160];
    }

    /// <summary>
    /// A typed name wins over a listed id. A name that matches the club list uses that rule set.
    /// Any other name is stored on the game and is not added to the club list.
    /// </summary>
    private async Task<(bool Ok, Guid? RuleSetId, string? CustomName, string? Error)> ResolveRuleSetAsync(
        Guid? ruleSetId,
        string? ruleSetName,
        CancellationToken cancellationToken)
    {
        var typed = Clean(ruleSetName);
        if (typed is not null)
        {
            var matchId = await db.RuleSets
                .Where(ruleSet => ruleSet.Name.ToLower() == typed.ToLower())
                .Select(ruleSet => (Guid?)ruleSet.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (matchId is Guid id)
                return (true, id, null, null);
            return (true, null, typed, null);
        }

        if (ruleSetId is Guid requested && !await db.RuleSets.AnyAsync(ruleSet => ruleSet.Id == requested, cancellationToken))
            return (false, null, null, "That rule set is not on the club list.");

        return (true, ruleSetId, null, null);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? GameOutsideDay(DateTime startUtc, DateTime endUtc, GameDay day)
    {
        if (endUtc < startUtc)
            return "End time must be after the start time.";
        if (startUtc < day.ScheduledAt || endUtc > day.EndsAt)
            return "Table times must stay inside the game day.";
        return null;
    }

    private static string FormatLocation(string? venue, string? address, string? locationNotes)
    {
        var location = string.Join(", ", new[] { venue, address }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(locationNotes))
            location = string.IsNullOrWhiteSpace(location) ? locationNotes! : $"{location} ({locationNotes})";
        return string.IsNullOrWhiteSpace(location) ? "Not listed" : location;
    }
}
