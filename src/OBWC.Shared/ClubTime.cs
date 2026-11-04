using System.Globalization;

namespace OBWC.Shared;

/// <summary>
/// Club events are stored in UTC and shown in America/Chicago, labeled CT
/// (Central Time covers both CST and CDT).
/// </summary>
public static class ClubTime
{
    public const string ZoneId = "America/Chicago";
    public const string WindowsZoneId = "Central Standard Time";
    public const string Label = "CT";

    private static readonly TimeZoneInfo Resolved = Resolve();

    public static TimeZoneInfo TimeZone => Resolved;

    public static DateTimeOffset ToClub(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Resolved);

    public static string FormatDate(DateTimeOffset instant) =>
        ToClub(instant).ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    public static string FormatLongDate(DateTimeOffset instant) =>
        ToClub(instant).ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);

    public static string FormatTime(DateTimeOffset instant) =>
        $"{ToClub(instant).ToString("h:mm tt", CultureInfo.InvariantCulture)} {Label}";

    public static string FormatTimeRange(DateTimeOffset start, DateTimeOffset? end)
    {
        var startText = ToClub(start).ToString("h:mm tt", CultureInfo.InvariantCulture);
        if (end is null)
            return $"{startText} {Label}";
        var endText = ToClub(end.Value).ToString("h:mm tt", CultureInfo.InvariantCulture);
        return $"{startText} – {endText} {Label}";
    }

    public static string FormatDateTime(DateTimeOffset instant) =>
        $"{FormatLongDate(instant)} · {FormatTime(instant)}";

    /// <summary>
    /// Interprets a date and time of day as a Central Time wall clock and returns the matching instant.
    /// </summary>
    public static DateTimeOffset FromClubWallClock(DateTime date, TimeSpan time)
    {
        var wall = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(wall, Resolved.GetUtcOffset(wall));
    }

    public static DateTime ClubToday() => ToClub(DateTimeOffset.UtcNow).Date;

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(WindowsZoneId);
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
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
