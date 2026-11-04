namespace OBWC.Api.Data.Seed;

/// <summary>
/// Facts copied from the club's page list. Nothing here is invented.
/// </summary>
public static class ClubContent
{
    public const string ContactEmail = "nmh16055@gmail.com";

    public const string AboutTitle = "About Us";

    public const string AboutBody =
        """
        Omaha Beach Wargaming Club (OBWC) is an organization whose purpose is to promote historical and miniature gaming. Our mission is to foster camaraderie, creativity, and strategic thinking through the immersive and diverse world of tabletop gaming. Dedicated to imaginative storytelling, we provide a welcoming community where enthusiasts of all ages and skill levels can come together to learn, compete, and share their passion for tabletop gaming. By promoting sportsmanship, inclusivity, and the pursuit of knowledge, we aim to inspire lifelong friendships and memorable experiences for all.

        OBWC is governed by member-elected officers serving on the Board of Directors (executive board). Terms of office are one year, with elections held at the May meeting. Dues are $25.00 per calendar year.
        """;

    public const string NukeConTitle = "Nuke-Con";

    public const string NukeConBody =
        "Bill Kuhfahl is the club's Nuke-Con liaison and maintains this page.";

    public static readonly IReadOnlyDictionary<string, string> Settings = new Dictionary<string, string>
    {
        [SettingKeys.ContactEmail] = ContactEmail,
        [SettingKeys.AdultDues] = "$25.00 per calendar year",
        [SettingKeys.StudentDues] = "$10.00",
        [SettingKeys.StudentDuesNote] = "22 and under",
        [SettingKeys.FriendDues] = "Free",
        [SettingKeys.OfficerTerm] = "One year",
        [SettingKeys.ElectionMeeting] = "May meeting"
    };

    public static readonly RuleSetSeed[] RuleSets =
    [
        new("Advanced Civilization", "Mike Guldemond, Len Hillhouse"),
        new("Baroque", "Steve Brady, Carl Linnerson"),
        new("Battlestations! Battlestations!", "Matt Johnson"),
        new("Blackpowder", "Steve Brady, Phil Myers, Mike Redelfs"),
        new("Brigade Fire & Fury", "Mike Redelfs"),
        new("Car Wars", "Erik Luken"),
        new("Command & Colors", "Bill Kuhfahl, Dave Richards, Mike Redelfs"),
        new("Command at Sea", "Mike Guldemond"),
        new("Command Decision", "Steve Brady, Matt Johnson, Phil Myers"),
        new("Conn! Sonar!", "Dave Moore"),
        new("Corps Command Napoleonics", "Matt Johnson"),
        new("De Bellis Antiquitas", "Steve Brady, Ryan Carlson, Mike Redelfs, Matt Johnson"),
        new("Eclectic Apocryphal Miniatures Games", "Jurgen Olk"),
        new("Empires in Arms", "Kurt Zachary, Bob Lingerfelt, William Saucier"),
        new("Europa Universalis", "Camden Olds"),
        new("Federation Commander", "Patrick Gude, Len Hillhouse, Matt Johnson"),
        new("Fire & Steel: Napoleonics", "Mike Guldemond"),
        new("Flames of War", "Steve Brady, Doug Carmichael, Kris Miller, Bill Kuhfahl"),
        new("Fly, Fight, Win: Warbirds", "Dave Moore"),
        new("General de Brigade", "Steve Brady, Lynn Langer"),
        new("General Quarters III", "Dave Moore"),
        new("Guild Ball", "Stephen Lambert"),
        new("Lost Battles", "Matt Johnson"),
        new("Mustangs & Messerschmitts", "Dave Moore, Lynn Langer"),
        new("Oathmark", "Ray Parrish"),
        new("PanzerKorps", "Matt Johnson"),
        new("Piquet/Archon 2", "Ryan Carlson"),
        new("Ravenfeast", "Ray Parrish, Bill Kuhfahl"),
        new("Ruthless", "Dave Moore"),
        new("Sail & Steam Navies", "Ryan Carlson"),
        new("Sharpe Practice", "Phil Myers, Len Hillhouse"),
        new("Sheetskrieg!", "Lynn Langer"),
        new("Siege of Jerusalem", "Mike Guldemond"),
        new("SPQR", "Mark Kyriss"),
        new("Star Wars Armada", "Jason Huffstetler, Kris Miller"),
        new("Team Yankee", "Mark Kyriss"),
        new("To the Strongest", "Mike Redelfs"),
        new("Warfare in the Age of Reason", "Steve Brady, Carl Linnerson, Mike Redelfs"),
        new("War Galley: Ancient Naval Combat", "Mike Guldemond, Mike Redelfs"),
        new("What A Tanker!", "Lynn Langer, Dave Moore"),
        new("Wings of Glory", "Ryan Carlson, Kevin Rush, Bill Kuhfahl, Doug Carmichael, David Abate"),
        new("Wooden ships & Iron Men", "Kurt Zachary"),
        new("World in Flames", "Patrick Gude, Kurt Zachary, Bob Lingerfelt")
    ];

    public sealed record RuleSetSeed(string Name, string PublishedPlayers);
}
