using OBWC.Shared;

namespace OBWC.Tests;

public class ClubTimeTests
{
    [Fact]
    public void Winter_utc_instant_displays_as_central_time()
    {
        var utc = new DateTimeOffset(2026, 1, 15, 0, 30, 0, TimeSpan.Zero);
        Assert.Equal("Wed 14 Jan 2026", ClubTime.FormatDate(utc));
        Assert.Equal("6:30 PM CT", ClubTime.FormatTime(utc));
        Assert.Contains("CT", ClubTime.FormatDateTime(utc), StringComparison.Ordinal);
        Assert.DoesNotContain("UTC", ClubTime.FormatTime(utc), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Summer_utc_instant_still_uses_the_ct_label()
    {
        var utc = new DateTimeOffset(2026, 7, 15, 0, 30, 0, TimeSpan.Zero);
        Assert.Equal("Tue 14 Jul 2026", ClubTime.FormatDate(utc));
        Assert.Equal("7:30 PM CT", ClubTime.FormatTime(utc));
    }

    [Fact]
    public void Wall_clock_in_central_time_is_stored_as_utc()
    {
        var winter = ClubTime.FromClubWallClock(new DateTime(2026, 1, 14), new TimeSpan(18, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), winter.ToUniversalTime());

        var summer = ClubTime.FromClubWallClock(new DateTime(2026, 7, 14), new TimeSpan(18, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 23, 0, 0, TimeSpan.Zero), summer.ToUniversalTime());
        Assert.Equal("6:00 PM CT", ClubTime.FormatTime(summer));
    }
}
