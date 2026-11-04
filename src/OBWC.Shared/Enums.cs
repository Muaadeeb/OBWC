using System.Text.Json.Serialization;

namespace OBWC.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipType
{
    Adult,
    Student,
    FriendOfObwc
}

/// <summary>Planning response before the night of play. Players do not set Waitlist directly.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionRsvpStatus
{
    Unknown = 0,
    Going = 1,
    Maybe = 2,
    NotGoing = 3,
    Waitlist = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendanceStatus
{
    Absent = 0,
    Present = 1,
    Excused = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentCategory
{
    Constitution,
    Budget
}
