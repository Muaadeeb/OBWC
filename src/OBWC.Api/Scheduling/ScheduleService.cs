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
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return (false, null, "User required.");
        if (endsAt is DateTimeOffset end && end < scheduledAt)
            return (false, null, "End time must be after the start time.");
        if (playerCap is int cap && cap < 1)
            return (false, null, "Player cap must be at least 1 (or blank for unlimited).");
        if (ruleSetId is Guid ruleId && !await db.RuleSets.AnyAsync(ruleSet => ruleSet.Id == ruleId, cancellationToken))
            return (false, null, "That rule set is not on the club list.");

        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            ScheduledAt = scheduledAt.UtcDateTime,
            EndsAt = endsAt?.UtcDateTime,
            Title = Clean(title),
            Notes = Clean(notes),
            Venue = Clean(venue),
            Address = Clean(address),
            OnlineLink = Clean(onlineLink),
            LocationNotes = Clean(locationNotes),
            PlayerCap = playerCap,
            RuleSetId = ruleSetId,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            IsPublished = isPublished,
            AllowPlayerRsvp = allowPlayerRsvp
        };

        foreach (var userId in await MemberIdsAsync(cancellationToken))
        {
            session.Attendances.Add(new SessionAttendance
            {
                Id = Guid.NewGuid(),
                PlaySessionId = session.Id,
                UserId = userId,
                Status = AttendanceStatus.Absent,
                RsvpStatus = SessionRsvpStatus.Unknown
            });
        }

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
                request.PlayerCap, request.RuleSetId, cancellationToken);
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
        db.PlaySessions.Include(session => session.Attendances).Include(session => session.RuleSet)
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
            session.Attendances.Add(row);
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
        if (request.RuleSetId is Guid ruleId && !await db.RuleSets.AnyAsync(ruleSet => ruleSet.Id == ruleId, cancellationToken))
            return (false, "That rule set is not on the club list.");

        var session = await db.PlaySessions.Include(item => item.Attendances)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
            return (false, "Event not found.");
        if (session.IsCancelled)
            return (false, "Cancelled events cannot be edited. Schedule a new one instead.");

        session.Title = Clean(request.Title);
        session.Notes = Clean(request.Notes);
        session.Venue = Clean(request.Venue);
        session.Address = Clean(request.Address);
        session.OnlineLink = Clean(request.OnlineLink);
        session.LocationNotes = Clean(request.LocationNotes);
        session.PlayerCap = request.PlayerCap;
        session.RuleSetId = request.RuleSetId;

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
            session.Attendances.Add(row);
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

        var root = await db.PlaySessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (root is null)
            return (false, "Event not found.");
        if (root.IsCancelled)
            return (false, "Cancelled events stay cancelled. Schedule a new one.");

        var oldStart = DateTime.SpecifyKind(root.ScheduledAt, DateTimeKind.Utc);
        var delta = newStart.UtcDateTime - oldStart;
        var targets = new List<PlaySession> { root };
        if (entireSeriesFollowing && root.SeriesKey is Guid seriesKey)
        {
            targets = await db.PlaySessions
                .Where(item => item.SeriesKey == seriesKey && item.ScheduledAt >= oldStart && !item.IsCancelled)
                .ToListAsync(cancellationToken);
        }

        foreach (var session in targets)
        {
            if (session.Id == root.Id)
            {
                session.ScheduledAt = newStart.UtcDateTime;
                session.EndsAt = newEnd?.UtcDateTime;
            }
            else
            {
                session.ScheduledAt = session.ScheduledAt.Add(delta);
                if (session.EndsAt is DateTime existingEnd)
                    session.EndsAt = existingEnd.Add(delta);
            }
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

    public async Task<int> SeedMemberOnUpcomingSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;
        var from = DateTime.UtcNow.AddHours(-6);
        var to = DateTime.UtcNow.AddDays(120);
        var sessionIds = await db.PlaySessions.AsNoTracking()
            .Where(session => session.IsPublished && !session.IsCancelled
                && session.ScheduledAt >= from
                && session.ScheduledAt <= to)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        var already = await db.SessionAttendances.AsNoTracking()
            .Where(row => sessionIds.Contains(row.PlaySessionId) && row.UserId == userId)
            .Select(row => row.PlaySessionId)
            .ToListAsync(cancellationToken);
        var have = already.ToHashSet();
        var added = 0;
        foreach (var sessionId in sessionIds)
        {
            if (!have.Add(sessionId))
                continue;
            db.SessionAttendances.Add(new SessionAttendance
            {
                Id = Guid.NewGuid(),
                PlaySessionId = sessionId,
                UserId = userId,
                Status = AttendanceStatus.Absent,
                RsvpStatus = SessionRsvpStatus.Unknown
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken);
        return added;
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
            var description = item.Notes ?? "";
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

    private async Task<List<string>> MemberIdsAsync(CancellationToken cancellationToken)
    {
        var roleIds = await db.Roles
            .Where(role => role.Name == RoleNames.Member || role.Name == RoleNames.Admin || role.Name == RoleNames.SuperAdmin)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        return await db.UserRoles.Where(link => roleIds.Contains(link.RoleId)).Select(link => link.UserId).Distinct().ToListAsync(cancellationToken);
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
        var location = string.Join(", ", new[] { session.Venue, session.Address }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(session.LocationNotes))
            location = string.IsNullOrWhiteSpace(location) ? session.LocationNotes! : $"{location} ({session.LocationNotes})";
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
            NoResponse = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Unknown),
            Waitlist = rows.Count(row => row.RsvpStatus == SessionRsvpStatus.Waitlist),
            MyRsvp = mine?.RsvpStatus ?? SessionRsvpStatus.Unknown,
            Present = rows.Count(row => row.Status == AttendanceStatus.Present),
            IAmCheckedIn = mine?.Status == AttendanceStatus.Present,
            NoShows = noShows,
            SeriesKey = session.SeriesKey,
            Venue = session.Venue,
            Address = session.Address,
            OnlineLink = session.OnlineLink,
            LocationNotes = session.LocationNotes,
            PlayerCap = session.PlayerCap,
            IsCancelled = session.IsCancelled,
            CancellationReason = session.CancellationReason,
            RuleSetId = session.RuleSetId,
            RuleSetName = session.RuleSet?.Name,
            OwnerUserId = session.CreatedByUserId,
            OwnerDisplayName = string.IsNullOrWhiteSpace(ownerName) ? "Member" : ownerName,
            OwnerIsGm = gmIds.Contains(session.CreatedByUserId),
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
            LocationDisplay = string.IsNullOrWhiteSpace(location) ? "Not listed" : location
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

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
