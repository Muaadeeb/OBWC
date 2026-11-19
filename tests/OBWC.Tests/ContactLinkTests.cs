using OBWC.Client.Components;

namespace OBWC.Tests;

public class ContactLinkTests
{
    [Fact]
    public void Store_address_opens_a_map_search()
    {
        var url = ContactLinks.ForAddress("794 Fort Crook Rd S, Bellevue, NE 68005");
        Assert.Equal(
            "https://www.google.com/maps/search/?api=1&query=794%20Fort%20Crook%20Rd%20S%2C%20Bellevue%2C%20NE%2068005",
            url);
    }

    [Theory]
    [InlineData("nmh16055@gmail.com", "mailto:nmh16055@gmail.com")]
    [InlineData("  President@OBWC.org ", "mailto:President@OBWC.org")]
    [InlineData("(402) 555-0100", "tel:4025550100")]
    [InlineData("+1 402 555 0100", "tel:+14025550100")]
    [InlineData("402.555.0100", "tel:4025550100")]
    [InlineData("https://example.com/club", "https://example.com/club")]
    public void A_whole_contact_line_becomes_a_link(string contact, string expected) =>
        Assert.Equal(expected, ContactLinks.ForContact(contact));

    [Theory]
    [InlineData("Ask at the store counter")]
    [InlineData("Room 12")]
    [InlineData("68005")]
    [InlineData("402-555-0100 ext 2")]
    [InlineData("1234567+")]
    [InlineData("+1+4025550100")]
    [InlineData("not-an-email")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Other_contact_wording_stays_plain_text(string contact) =>
        Assert.Null(ContactLinks.ForContact(contact));
}
