namespace OBWC.Client.Components;

/// <summary>
/// Map search for a public address. An officer contact becomes a mail, phone, or web link
/// only when the whole line is that kind of address. Any other wording stays plain text.
/// </summary>
public static class ContactLinks
{
    public static string ForAddress(string address) =>
        "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(address.Trim());

    public static string? ForContact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (IsEmail(text))
            return "mailto:" + text;
        if (IsWeb(text))
            return text;
        if (TryPhone(text, out var tel))
            return tel;
        return null;
    }

    private static bool IsEmail(string text)
    {
        if (text.Length > 200)
            return false;

        var at = text.IndexOf('@');
        if (at <= 0 || at != text.LastIndexOf('@') || at >= text.Length - 1)
            return false;

        var local = text[..at];
        var domain = text[(at + 1)..];
        var dot = domain.IndexOf('.');
        if (dot <= 0 || dot >= domain.Length - 1)
            return false;
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal))
            return false;
        if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (var ch in text)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '@' or '.' or '_' or '+' or '-' or '%')
                continue;
            return false;
        }

        return true;
    }

    private static bool IsWeb(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;
        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }

    private static bool TryPhone(string text, out string tel)
    {
        tel = "";
        var plus = text[0] == '+';
        var digits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsDigit(ch))
            {
                digits++;
                continue;
            }
            if (ch == '+')
            {
                if (i != 0)
                    return false;
                continue;
            }
            if (ch is ' ' or '(' or ')' or '-' or '.')
                continue;
            return false;
        }

        if (digits is < 7 or > 15)
            return false;

        var dial = new string(text.Where(char.IsDigit).ToArray());
        tel = plus ? "tel:+" + dial : "tel:" + dial;
        return true;
    }
}
