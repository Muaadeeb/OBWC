using System.ComponentModel.DataAnnotations;

namespace OBWC.Shared.Dtos;

public record CreateSessionRequest
{
    public DateTimeOffset ScheduledAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }

    [MaxLength(160)]
    public string? Title { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MaxLength(160)]
    public string? Venue { get; init; }

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(500)]
    public string? OnlineLink { get; init; }

    [MaxLength(1000)]
    public string? LocationNotes { get; init; }

    public int? PlayerCap { get; init; }
    public bool IsPublished { get; init; } = true;
    public bool AllowPlayerRsvp { get; init; } = true;

    [Range(1, 26)]
    public int Weeks { get; init; } = 1;

    public Guid? RuleSetId { get; init; }
}

public record UpdateSessionDetailsRequest
{
    [MaxLength(160)]
    public string? Title { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MaxLength(160)]
    public string? Venue { get; init; }

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(500)]
    public string? OnlineLink { get; init; }

    [MaxLength(1000)]
    public string? LocationNotes { get; init; }

    public int? PlayerCap { get; init; }
    public Guid? RuleSetId { get; init; }
}

public record CancelSessionRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }

    public bool EntireSeries { get; init; }
    public bool Notify { get; init; } = true;
}

public record RescheduleSessionRequest
{
    public DateTimeOffset NewStart { get; init; }
    public DateTimeOffset? NewEnd { get; init; }
    public bool EntireSeriesFollowing { get; init; }
    public bool Notify { get; init; } = true;
}

public record SetRsvpRequest
{
    public SessionRsvpStatus Status { get; init; }
}

public record RsvpResultDto
{
    public bool Ok { get; init; }
    public SessionRsvpStatus Status { get; init; }
    public string? Note { get; init; }
}

public record CheckInRequest
{
    public string? UserId { get; init; }
}

public record PublishSessionRequest
{
    public bool Published { get; init; }
}

public record CalendarEventDto
{
    public Guid Id { get; init; }
    public DateTimeOffset ScheduledAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }
    public string? Title { get; init; }
    public string? Notes { get; init; }
    public bool IsPublished { get; init; }
    public bool AllowPlayerRsvp { get; init; }
    public int Going { get; init; }
    public int Maybe { get; init; }
    public int NotGoing { get; init; }
    public int NoResponse { get; init; }
    public int Waitlist { get; init; }
    public SessionRsvpStatus MyRsvp { get; init; }
    public int Present { get; init; }
    public bool IAmCheckedIn { get; init; }
    public int NoShows { get; init; }
    public Guid? SeriesKey { get; init; }
    public string? Venue { get; init; }
    public string? Address { get; init; }
    public string? OnlineLink { get; init; }
    public string? LocationNotes { get; init; }
    public int? PlayerCap { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public Guid? RuleSetId { get; init; }
    public string? RuleSetName { get; init; }
    public string OwnerUserId { get; init; } = "";
    public string OwnerDisplayName { get; init; } = "";
    public bool OwnerIsGm { get; init; }
    public bool CanEdit { get; init; }
    public string LocationDisplay { get; init; } = "";
}

public record RosterRowDto
{
    public string UserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Email { get; init; } = "";
    public SessionRsvpStatus Rsvp { get; init; }
    public DateTimeOffset? RsvpAt { get; init; }
    public AttendanceStatus NightStatus { get; init; }
    public DateTimeOffset? CheckedInAt { get; init; }
}

public record CreateSeriesResultDto
{
    public int Created { get; init; }
    public Guid? FirstSessionId { get; init; }
}

public record SaveRuleSetRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = "";

    [MaxLength(2000)]
    public string? PublishedPlayers { get; init; }

    public int SortOrder { get; init; }
}

public record RuleSetDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? PublishedPlayers { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyList<RuleSetPlayerDto> Players { get; init; } = [];
}

public record RuleSetPlayerDto
{
    public Guid Id { get; init; }
    public string OwnerUserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Note { get; init; }
    public bool CanEdit { get; init; }
}

public record SavePlayerRequest
{
    [MaxLength(500)]
    public string? Note { get; init; }
}
