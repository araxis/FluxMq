using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class AppThemeService : IDisposable
{
    public ThemePreference Preference { get; private set; } = ThemePreference.System;
    public bool IsDarkMode => Preference switch
    {
        ThemePreference.Dark => true,
        ThemePreference.Light => false,
        _ => Application.Current?.RequestedTheme == AppTheme.Dark
    };

    public event EventHandler? Changed;

    public AppThemeService()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }
    }

    public void SetPreference(ThemePreference preference)
    {
        if (Preference == preference)
        {
            return;
        }

        Preference = preference;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (Preference == ThemePreference.System)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
