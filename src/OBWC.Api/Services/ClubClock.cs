namespace OBWC.Api.Services;

public static class ClubClock
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    public static DateTime Now() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly Today() => DateOnly.FromDateTime(Now());

    public static TimeOnly TimeOfDay() => TimeOnly.FromDateTime(Now());

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
