using System.Windows;
using DocumentTranslator.Services;

namespace DocumentTranslator;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
        ThemeManager.ApplyTitleBar(this);
        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;

        var settings = SettingsStore.Load();
        EndpointBox.Text = settings.Endpoint;
        var key = SettingsStore.UnprotectKey(settings);
        if (!string.IsNullOrEmpty(key))
        {
            KeyBox.Password = key;
        }
    }

    private void OnThemeChanged(object? sender, System.EventArgs e)
    {
        ThemeManager.ApplyTitleBar(this);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsStore.SaveCredentials(EndpointBox.Text, KeyBox.Password);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed to save: " + ex.Message;
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        StatusText.Text = "Testing...";
        try
        {
            var svc = new TranslatorService(EndpointBox.Text, KeyBox.Password);
            var langs = await svc.GetLanguagesAsync();
            StatusText.Text = $"OK — {langs.Count} languages available.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Test failed: " + ex.Message;
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }
}
