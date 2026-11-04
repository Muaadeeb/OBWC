using System.ComponentModel.DataAnnotations;

namespace OBWC.Shared.Dtos;

public record PublicInfoDto
{
    public string ContactEmail { get; init; } = "";
    public string AdultDues { get; init; } = "";
    public string StudentDues { get; init; } = "";
    public string StudentDuesNote { get; init; } = "";
    public string FriendDues { get; init; } = "";
    public string OfficerTerm { get; init; } = "";
    public string ElectionMeeting { get; init; } = "";
    public DateOnly ClubToday { get; init; }
}

public record UpdatePublicInfoRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string ContactEmail { get; init; } = "";

    [Required, MaxLength(100)]
    public string AdultDues { get; init; } = "";

    [Required, MaxLength(100)]
    public string StudentDues { get; init; } = "";

    [Required, MaxLength(100)]
    public string StudentDuesNote { get; init; } = "";

    [Required, MaxLength(100)]
    public string FriendDues { get; init; } = "";

    [Required, MaxLength(100)]
    public string OfficerTerm { get; init; } = "";

    [Required, MaxLength(100)]
    public string ElectionMeeting { get; init; } = "";
}

public record ContentPageDto
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTime UpdatedAtUtc { get; init; }
}

public record SaveContentRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = "";

    [Required, MaxLength(20000)]
    public string Body { get; init; } = "";
}

public record ContactRequest
{
    [Required(ErrorMessage = "Enter your name."), MaxLength(100)]
    public string Name { get; init; } = "";

    [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(256)]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Enter a message."), MaxLength(4000)]
    public string Message { get; init; } = "";

    [MaxLength(200)]
    public string? Website { get; init; }
}

public record SaveOfficerRequest
{
    [Required, MaxLength(100)]
    public string RoleTitle { get; init; } = "";

    [Required, MaxLength(100)]
    public string DisplayName { get; init; } = "";

    [MaxLength(100)]
    public string? TermLabel { get; init; }

    public int SortOrder { get; init; }
}

public record OfficerDto
{
    public Guid Id { get; init; }
    public string RoleTitle { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? TermLabel { get; init; }
    public int SortOrder { get; init; }
}

public record SaveStoreRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = "";

    [MaxLength(200)]
    public string? FormerName { get; init; }

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    [MaxLength(300)]
    public string? Bullet1 { get; init; }

    [MaxLength(300)]
    public string? Bullet2 { get; init; }

    [MaxLength(300)]
    public string? Bullet3 { get; init; }

    public int SortOrder { get; init; }
}

public record StoreDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? FormerName { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public string? Bullet1 { get; init; }
    public string? Bullet2 { get; init; }
    public string? Bullet3 { get; init; }
    public int SortOrder { get; init; }
}

public record SaveVenueRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = "";

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    public int SortOrder { get; init; }
}

public record VenueDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Address { get; init; }
    public string? Description { get; init; }
    public bool HasInsidePhoto { get; init; }
    public bool HasOutsidePhoto { get; init; }
    public string? InsidePhotoUrl { get; init; }
    public string? OutsidePhotoUrl { get; init; }
    public int SortOrder { get; init; }
}

public record SaveGroupRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = "";

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    public int SortOrder { get; init; }
}

public record GroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? WebsiteUrl { get; init; }
    public int SortOrder { get; init; }
}

public record SaveConventionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = "";

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(200)]
    public string? WhenText { get; init; }

    [MaxLength(300)]
    public string? LocationText { get; init; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    public int SortOrder { get; init; }
}

public record ConventionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? WhenText { get; init; }
    public string? LocationText { get; init; }
    public string? WebsiteUrl { get; init; }
    public int SortOrder { get; init; }
}

public record GalleryPhotoDto
{
    public Guid Id { get; init; }
    public string Caption { get; init; } = "";
    public string PublicEventName { get; init; } = "";
    public string Url { get; init; } = "";
    public DateTime CreatedAtUtc { get; init; }
    public bool CanEdit { get; init; }
}

public record GalleryListDto
{
    public int Count { get; init; }
    public int MaxPhotos { get; init; }
    public IReadOnlyList<GalleryPhotoDto> Photos { get; init; } = [];
}

public record ClubDocumentDto
{
    public Guid Id { get; init; }
    public DocumentCategory Category { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string FileName { get; init; } = "";
    public DateTime CreatedAtUtc { get; init; }
}
