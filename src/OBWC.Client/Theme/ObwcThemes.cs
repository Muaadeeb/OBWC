using MudBlazor;

namespace OBWC.Client.Theme;

public static class ObwcThemes
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#7A8F4A",
            PrimaryContrastText = "#F7F4EC",
            Secondary = "#8B6B45",
            SecondaryContrastText = "#F7F4EC",
            Tertiary = "#C9B873",
            TertiaryContrastText = "#2C261C",
            Info = "#2F6DB5",
            InfoContrastText = "#F7F4EC",
            AppbarBackground = "#3E4A28",
            AppbarText = "#F4F0E4",
            Background = "#F7F4EC",
            Surface = "#FFFDF8",
            DrawerBackground = "#F3EFE3",
            DrawerText = "#2C261C",
            DrawerIcon = "#5C5144",
            TextPrimary = "#2C261C",
            TextSecondary = "#5C5144",
            ActionDefault = "#5C5144",
            Divider = "#E4D7BC",
            LinesDefault = "#E4D7BC",
            Success = "#4E7A3A",
            Warning = "#A37B32"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A3B56E",
            PrimaryContrastText = "#141910",
            Secondary = "#C4A27A",
            SecondaryContrastText = "#141910",
            Tertiary = "#C9B873",
            TertiaryContrastText = "#141910",
            Info = "#6FA2D8",
            InfoContrastText = "#0E141C",
            AppbarBackground = "#141910",
            AppbarText = "#F4F0E4",
            Background = "#141910",
            Surface = "#1E2618",
            DrawerBackground = "#1A2114",
            DrawerText = "#F3EFE3",
            DrawerIcon = "#C9B873",
            TextPrimary = "#F3EFE3",
            TextSecondary = "#D9CDB4",
            ActionDefault = "#D9CDB4",
            Divider = "#3A4630",
            LinesDefault = "#3A4630",
            Success = "#8FB56A",
            Warning = "#E0C078"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Segoe UI", "Helvetica Neue", "Arial", "sans-serif"]
            }
        }
    };
}
