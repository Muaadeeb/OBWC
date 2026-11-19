namespace OBWC.Client.Scheduling;

/// <summary>Opens one venue on the public venues list.</summary>
public static class VenueLinks
{
    public static string Page(Guid id) => $"/venues#{Element(id)}";

    public static string Element(Guid id) => $"venue-{id:D}";
}
