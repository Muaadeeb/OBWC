using System.Globalization;
using OBWC.Shared;

namespace OBWC.Client.Scheduling;

/// <summary>Addresses that open one game, game day, or convention on its list.</summary>
public static class ScheduleLinks
{
    public static string Game(Guid id, DateTimeOffset scheduledAt) =>
        $"/schedule?game={id:D}&month={MonthKey(scheduledAt)}";

    public static string Day(Guid id, DateTimeOffset scheduledAt) =>
        $"/schedule?day={id:D}&month={MonthKey(scheduledAt)}";

    public static string Convention(Guid id) => $"/conventions?id={id:D}";

    public static string GameElement(Guid id) => $"game-{id:D}";

    public static string DayElement(Guid id) => $"day-{id:D}";

    public static string ConventionElement(Guid id) => $"convention-{id:D}";

    public static string MonthKey(DateTimeOffset scheduledAt) =>
        ClubTime.ToClub(scheduledAt).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static bool TryParseMonth(string? value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Split('-', 2);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out month)
            && year is >= 2000 and <= 2100
            && month is >= 1 and <= 12;
    }
}
