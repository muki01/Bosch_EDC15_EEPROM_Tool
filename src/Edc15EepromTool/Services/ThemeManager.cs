using System.Windows;
using Microsoft.Win32;

namespace Edc15EepromTool.Services;

/// <summary>
/// Follows the Windows app mode (Settings → Personalization → Colors): light or dark.
/// The colour dictionary is swapped at run time, so every <c>DynamicResource</c> brush updates at once.
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Uri DarkColors = new("pack://application:,,,/Themes/Colors.Dark.xaml");
    private static readonly Uri LightColors = new("pack://application:,,,/Themes/Colors.Light.xaml");

    public static bool IsLight { get; private set; }

    /// <summary>Applies the current Windows mode and keeps following it until the application exits.</summary>
    public static void FollowSystem(Application application)
    {
        Apply(SystemUsesLightTheme());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        application.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static void Apply(bool light)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        // the colour dictionary is always the first one, see App.xaml
        dictionaries[0] = new ResourceDictionary { Source = light ? LightColors : DarkColors };
        IsLight = light;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        var light = SystemUsesLightTheme();
        if (light != IsLight)
        {
            Application.Current.Dispatcher.Invoke(() => Apply(light));
        }
    }

    private static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        // the value is missing on older Windows versions, which only have a light mode
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}
