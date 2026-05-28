using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DocumentTranslator.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static AppTheme _current = AppTheme.System;
    private static bool _listeningToSystem;

    public static AppTheme Current => _current;

    public static event EventHandler? ThemeChanged;

    public static void Apply(AppTheme theme)
    {
        _current = theme;

        var effective = theme == AppTheme.System ? DetectSystemTheme() : theme;
        ApplyEffective(effective);
        ApplyTitleBarToAllWindows(effective);

        if (theme == AppTheme.System)
        {
            HookSystemThemeChanges();
        }
        else
        {
            UnhookSystemThemeChanges();
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static AppTheme EffectiveTheme =>
        _current == AppTheme.System ? DetectSystemTheme() : _current;

    public static void ApplyTitleBar(Window window)
    {
        if (window is null)
        {
            return;
        }

        void Apply()
        {
            ApplyTitleBarCore(window, EffectiveTheme);
        }

        if (window.IsLoaded)
        {
            Apply();
        }
        else
        {
            window.SourceInitialized += (_, _) => Apply();
        }
    }

    private static void ApplyTitleBarToAllWindows(AppTheme effective)
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBarCore(window, effective);
        }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static void ApplyTitleBarCore(Window window, AppTheme effective)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            int useDark = effective == AppTheme.Dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
            // Best effort — older Windows builds may not support this attribute.
        }
    }

    public static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
            {
                return i == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
            // Fall through to default.
        }
        return AppTheme.Light;
    }

    private static void ApplyEffective(AppTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var source = theme == AppTheme.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var dict = new ResourceDictionary { Source = source };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
        {
            merged[0] = dict;
        }
        else
        {
            merged.Add(dict);
        }
    }

    private static void HookSystemThemeChanges()
    {
        if (_listeningToSystem)
        {
            return;
        }
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _listeningToSystem = true;
    }

    private static void UnhookSystemThemeChanges()
    {
        if (!_listeningToSystem)
        {
            return;
        }
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _listeningToSystem = false;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _current != AppTheme.System)
        {
            return;
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var effective = DetectSystemTheme();
            ApplyEffective(effective);
            ApplyTitleBarToAllWindows(effective);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        });
    }
}
