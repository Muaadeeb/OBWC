namespace OBWC.Api.Services;

public static class ClubClock
{
    public static TimeZoneInfo Zone => ClubTime.TimeZone;

    public static DateTime Now() => ClubTime.ToClub(DateTimeOffset.UtcNow).DateTime;

    public static DateOnly Today() => DateOnly.FromDateTime(ClubTime.ClubToday());

    public static TimeOnly TimeOfDay() => TimeOnly.FromDateTime(Now());
}
