using OBWC.Shared;

namespace OBWC.Api.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public MembershipType? MembershipType { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByHash { get; set; }
}

public interface IOwnedResource
{
    string? OwnerUserId { get; }
}

/// <summary>
/// One scheduled club game night. This is the schedule module's event, ported from the
/// campaign play-session calendar (no campaign, contract, or award fields).
/// </summary>
public class PlaySession : IOwnedResource
{
    public Guid Id { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public string? Venue { get; set; }
    public string? Address { get; set; }
    public string? OnlineLink { get; set; }
    public string? LocationNotes { get; set; }
    public int? PlayerCap { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool AllowPlayerRsvp { get; set; } = true;
    public bool IsCancelled { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? SeriesKey { get; set; }
    public Guid? RuleSetId { get; set; }
    public RuleSet? RuleSet { get; set; }
    public List<SessionAttendance> Attendances { get; set; } = [];
    public List<EventPhoto> Photos { get; set; } = [];

    string? IOwnedResource.OwnerUserId => CreatedByUserId;
}

public class SessionAttendance
{
    public Guid Id { get; set; }
    public Guid PlaySessionId { get; set; }
    public PlaySession? PlaySession { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Absent;
    public SessionRsvpStatus RsvpStatus { get; set; } = SessionRsvpStatus.Unknown;
    public DateTime? RsvpAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckedInByUserId { get; set; }
    public string? Notes { get; set; }
    public string? MarkedByUserId { get; set; }
    public DateTime? MarkedAt { get; set; }
}

public class RuleSet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? PublishedPlayers { get; set; }
    public int SortOrder { get; set; }
    public List<RuleSetPlayer> Players { get; set; } = [];
}

public class RuleSetPlayer : IOwnedResource
{
    public Guid Id { get; set; }
    public Guid RuleSetId { get; set; }
    public RuleSet? RuleSet { get; set; }
    public string OwnerUserId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    string? IOwnedResource.OwnerUserId => OwnerUserId;
}

public class GameStore
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? FormerName { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? Bullet1 { get; set; }
    public string? Bullet2 { get; set; }
    public string? Bullet3 { get; set; }
    public int SortOrder { get; set; }
}

public class Venue
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? InsidePhotoPath { get; set; }
    public string? InsideContentType { get; set; }
    public string? OutsidePhotoPath { get; set; }
    public string? OutsideContentType { get; set; }
    public int SortOrder { get; set; }
}

public class GamingGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? GameFocus { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Contact { get; set; }
    public int SortOrder { get; set; }
}

public class Convention
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Calendar date the convention starts. Not a clock time.</summary>
    public DateOnly? StartsOn { get; set; }
    /// <summary>Calendar date the convention ends, when it spans more than one day.</summary>
    public DateOnly? EndsOn { get; set; }
    public string? LocationText { get; set; }
    public string? WebsiteUrl { get; set; }
    public int SortOrder { get; set; }
}

public class EventPhoto : IOwnedResource
{
    public Guid Id { get; set; }
    public Guid PlaySessionId { get; set; }
    public PlaySession? PlaySession { get; set; }
    public string Caption { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    string? IOwnedResource.OwnerUserId => OwnerUserId;
}

public class GalleryPhoto : IOwnedResource
{
    public Guid Id { get; set; }
    public string Caption { get; set; } = "";
    public string PublicEventName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    string? IOwnedResource.OwnerUserId => OwnerUserId;
}

public class ClubDocument
{
    public Guid Id { get; set; }
    public DocumentCategory Category { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.MembersOnly;
    public string OwnerUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public class ContentPage
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}

public class SiteSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class Officer
{
    public Guid Id { get; set; }
    public string RoleTitle { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? TermLabel { get; set; }
    public string? Contact { get; set; }
    public string? PhotoPath { get; set; }
    public string? PhotoContentType { get; set; }
    public int SortOrder { get; set; }
}
