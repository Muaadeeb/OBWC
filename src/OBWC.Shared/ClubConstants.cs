namespace OBWC.Shared;

public static class RoleNames
{
    public const string Member = "Member";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Gm = "GM";

    public static readonly IReadOnlyList<string> All = [Member, Admin, SuperAdmin, Gm];
}

public static class PolicyNames
{
    public const string Member = "Member";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string OwnerOrAdmin = "OwnerOrAdmin";
}

public static class GalleryLimits
{
    public const int MaxPhotos = 100;
}

public static class ContentKeys
{
    public const string About = "about";
    public const string NukeCon = "nuke-con";

    public static bool IsKnown(string? key) =>
        string.Equals(key, About, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, NukeCon, StringComparison.OrdinalIgnoreCase);
}

public static class SettingKeys
{
    public const string ContactEmail = "ContactEmail";
    public const string AdultDues = "AdultDues";
    public const string StudentDues = "StudentDues";
    public const string StudentDuesNote = "StudentDuesNote";
    public const string FriendDues = "FriendDues";
    public const string OfficerTerm = "OfficerTerm";
    public const string ElectionMeeting = "ElectionMeeting";

    public static readonly IReadOnlyList<string> All =
    [
        ContactEmail,
        AdultDues,
        StudentDues,
        StudentDuesNote,
        FriendDues,
        OfficerTerm,
        ElectionMeeting
    ];
}
