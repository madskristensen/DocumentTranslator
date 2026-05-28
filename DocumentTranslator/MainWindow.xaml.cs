using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DocumentTranslator.Models;
using DocumentTranslator.Services;
using Microsoft.Win32;

namespace DocumentTranslator;

public partial class MainWindow : Window
{
    private static readonly Language AutoDetect = new("", "Auto-detect (not recommended)");

    private readonly ObservableCollection<string> _progress = new();
    private TranslatorService? _translator;
    private IReadOnlyList<Language> _languages = Array.Empty<Language>();

    public MainWindow()
    {
        InitializeComponent();
        ProgressList.ItemsSource = _progress;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!EnsureTranslator(promptIfMissing: true))
        {
            return;
        }

        await LoadLanguagesAsync();
    }

    private bool EnsureTranslator(bool promptIfMissing)
    {
        var settings = SettingsStore.Load();
        var key = SettingsStore.UnprotectKey(settings);

        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(key))
        {
            if (promptIfMissing)
            {
                MessageBox.Show(this,
                    "Configure your Azure Translator endpoint and key in Settings (gear icon).",
                    "Document Translator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                OpenSettings();
                return _translator is not null;
            }
            return false;
        }

        try
        {
            _translator = new TranslatorService(settings.Endpoint, key);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Invalid settings: " + ex.Message, "Document Translator",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task LoadLanguagesAsync()
    {
        if (_translator is null)
        {
            return;
        }

        try
        {
            _languages = await _translator.GetLanguagesAsync();
        }
        catch
        {
            _languages = TranslatorService.GetFallbackLanguages();
        }

        var sources = new List<Language> { AutoDetect };
        sources.AddRange(_languages);
        SourceLanguageBox.ItemsSource = sources;
        TargetLanguagesList.ItemsSource = _languages;

        var settings = SettingsStore.Load();

        var savedSource = sources.FirstOrDefault(l => l.Code.Equals(settings.LastSourceLanguage ?? "", StringComparison.OrdinalIgnoreCase));
        SourceLanguageBox.SelectedItem = savedSource
            ?? sources.FirstOrDefault(l => l.Code.Equals("en", StringComparison.OrdinalIgnoreCase))
            ?? AutoDetect;

        TargetLanguagesList.SelectedItems.Clear();
        if (settings.LastTargetLanguages.Count > 0)
        {
            var savedSet = new HashSet<string>(settings.LastTargetLanguages, StringComparer.OrdinalIgnoreCase);
            foreach (var lang in _languages.Where(l => savedSet.Contains(l.Code)))
            {
                TargetLanguagesList.SelectedItems.Add(lang);
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out _)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (TryGetDroppedFile(e, out var path))
        {
            FilePathBox.Text = path;
        }
    }

    private static bool TryGetDroppedFile(System.Windows.DragEventArgs e, out string path)
    {
        path = string.Empty;
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return false;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return false;
        }

        var first = files[0];
        if (!File.Exists(first))
        {
            return false;
        }

        path = first;
        return true;
    }

    private async void OpenSettings()
    {
        var dlg = new SettingsDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (EnsureTranslator(promptIfMissing: false))
            {
                await LoadLanguagesAsync();
            }
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Pick a document to translate",
            Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|" +
                     "Word (*.docx)|*.docx|" +
                     "PowerPoint (*.pptx)|*.pptx|" +
                     "Excel (*.xlsx)|*.xlsx|" +
                     "HTML (*.html;*.htm)|*.html;*.htm|" +
                     "Text (*.txt)|*.txt|" +
                     "PDF (*.pdf)|*.pdf|" +
                     "All files (*.*)|*.*",
        };

        if (!string.IsNullOrEmpty(FilePathBox.Text) && File.Exists(FilePathBox.Text))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(FilePathBox.Text);
            dlg.FileName = Path.GetFileName(FilePathBox.Text);
        }

        if (dlg.ShowDialog(this) == true)
        {
            FilePathBox.Text = dlg.FileName;
        }
    }

    private async void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_translator is null)
        {
            if (!EnsureTranslator(promptIfMissing: true))
            {
                return;
            }
        }

        var filePath = FilePathBox.Text;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show(this, "Pick a source document first.", "Translate",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var targets = TargetLanguagesList.SelectedItems.Cast<Language>().ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Select at least one target language.", "Translate",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var source = SourceLanguageBox.SelectedItem as Language;
        var sourceCode = string.IsNullOrEmpty(source?.Code) ? null : source.Code;

        try
        {
            SettingsStore.SaveLanguageSelection(sourceCode, targets.Select(t => t.Code));
        }
        catch
        {
            // Best-effort.
        }

        TranslateButton.IsEnabled = false;
        _progress.Clear();
        foreach (var t in targets)
        {
            _progress.Add($"{t.Code}: queued");
        }

        var progress = new Progress<(string Code, string Status)>(p =>
        {
            for (int i = 0; i < _progress.Count; i++)
            {
                if (_progress[i].StartsWith(p.Code + ":", StringComparison.Ordinal))
                {
                    _progress[i] = $"{p.Code}: {p.Status}";
                    return;
                }
            }
        });

        var tasks = targets.Select(t => RunOne(filePath, t.Code, sourceCode, progress)).ToArray();
        await Task.WhenAll(tasks);

        TranslateButton.IsEnabled = true;
    }

    private async Task RunOne(string filePath, string targetCode, string? sourceCode, IProgress<(string, string)> progress)
    {
        progress.Report((targetCode, "translating..."));
        try
        {
            var output = await Task.Run(() => _translator!.TranslateFileAsync(filePath, sourceCode, targetCode));
            progress.Report((targetCode, $"done → {Path.GetFileName(output)}"));
        }
        catch (Exception ex)
        {
            progress.Report((targetCode, "failed: " + ex.Message));
        }
    }
}
