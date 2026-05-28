using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.AI.Translation.Document;
using DocumentTranslator.Models;
using Markdig;

namespace DocumentTranslator.Services;

public sealed class TranslatorService
{
    private readonly Uri _endpoint;
    private readonly AzureKeyCredential _credential;
    private readonly SingleDocumentTranslationClient _client;

    public TranslatorService(string endpoint, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Key is required.", nameof(apiKey));
        }

        _endpoint = new Uri(endpoint);
        _credential = new AzureKeyCredential(apiKey);
        _client = new SingleDocumentTranslationClient(_endpoint, _credential);
    }

    public async Task<IReadOnlyList<Language>> GetLanguagesAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient();
        var url = "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation";
        using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var list = new List<Language>();
        if (doc.RootElement.TryGetProperty("translation", out var translation))
        {
            foreach (var prop in translation.EnumerateObject())
            {
                var code = prop.Name;
                var name = prop.Value.TryGetProperty("name", out var n) ? n.GetString() ?? code : code;
                list.Add(new Language(code, name));
            }
        }

        return list.OrderBy(l => l.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static IReadOnlyList<Language> GetFallbackLanguages() => new[]
    {
        new Language("en", "English"),
        new Language("es", "Spanish"),
        new Language("fr", "French"),
        new Language("de", "German"),
        new Language("it", "Italian"),
        new Language("pt", "Portuguese"),
        new Language("nl", "Dutch"),
        new Language("sv", "Swedish"),
        new Language("da", "Danish"),
        new Language("nb", "Norwegian"),
        new Language("fi", "Finnish"),
        new Language("pl", "Polish"),
        new Language("ru", "Russian"),
        new Language("ja", "Japanese"),
        new Language("ko", "Korean"),
        new Language("zh-Hans", "Chinese (Simplified)"),
        new Language("zh-Hant", "Chinese (Traditional)"),
        new Language("ar", "Arabic"),
        new Language("hi", "Hindi"),
    };

    public async Task<string> TranslateFileAsync(
        string sourceFile,
        string? sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException("Source file not found.", sourceFile);
        }

        var ext = Path.GetExtension(sourceFile);
        var isMarkdown = ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                      || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);

        if (isMarkdown)
        {
            return await TranslateMarkdownAsync(sourceFile, sourceLanguage, targetLanguage, ct).ConfigureAwait(false);
        }

        await using var stream = File.OpenRead(sourceFile);
        var fileName = Path.GetFileName(sourceFile);
        var contentType = GuessContentType(sourceFile);

        var document = new MultipartFormFileData(fileName, stream, contentType);
        var content = new DocumentTranslateContent(document);

        Response<BinaryData> response = await _client.TranslateAsync(
            targetLanguage,
            content,
            sourceLanguage: string.IsNullOrWhiteSpace(sourceLanguage) ? null : sourceLanguage,
            cancellationToken: ct).ConfigureAwait(false);

        var dir = Path.GetDirectoryName(sourceFile) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(sourceFile);
        var outPath = Path.Combine(dir, $"{baseName}.{targetLanguage}{ext}");

        await File.WriteAllBytesAsync(outPath, response.Value.ToArray(), ct).ConfigureAwait(false);
        return outPath;
    }

    private async Task<string> TranslateMarkdownAsync(
        string sourceFile,
        string? sourceLanguage,
        string targetLanguage,
        CancellationToken ct)
    {
        var markdown = await File.ReadAllTextAsync(sourceFile, ct).ConfigureAwait(false);

        // Strip YAML frontmatter so it isn't mangled by translation, and re-prepend it verbatim.
        var (frontmatter, body) = ExtractYamlFrontmatter(markdown);

        var pipeline = new Markdig.MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        var html = Markdig.Markdown.ToHtml(body, pipeline);

        // Wrap in a minimal HTML document so the service treats it as text/html cleanly.
        var wrapped = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>" + html + "</body></html>";

        var bytes = System.Text.Encoding.UTF8.GetBytes(wrapped);
        using var memStream = new MemoryStream(bytes);
        var document = new MultipartFormFileData(
            Path.GetFileNameWithoutExtension(sourceFile) + ".html",
            memStream,
            "text/html");
        var content = new DocumentTranslateContent(document);

        Response<BinaryData> response = await _client.TranslateAsync(
            targetLanguage,
            content,
            sourceLanguage: string.IsNullOrWhiteSpace(sourceLanguage) ? null : sourceLanguage,
            cancellationToken: ct).ConfigureAwait(false);

        var translatedHtml = response.Value.ToString();

        var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
        {
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
        });
        new HtmlBlockPassthroughConverter(converter, "details", recurseChildren: true);
        new HtmlBlockPassthroughConverter(converter, "summary", recurseChildren: false);
        var translatedMarkdown = converter.Convert(translatedHtml);

        if (!string.IsNullOrEmpty(frontmatter))
        {
            // Ensure a blank line between frontmatter and the translated body.
            if (!frontmatter.EndsWith("\n\n", StringComparison.Ordinal)
                && !frontmatter.EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                var newline = frontmatter.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                if (!frontmatter.EndsWith(newline, StringComparison.Ordinal))
                {
                    frontmatter += newline;
                }
                frontmatter += newline;
            }
            translatedMarkdown = frontmatter + translatedMarkdown.TrimStart('\r', '\n');
        }

        var dir = Path.GetDirectoryName(sourceFile) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(sourceFile);
        var ext = Path.GetExtension(sourceFile);
        var outPath = Path.Combine(dir, $"{baseName}.{targetLanguage}{ext}");

        await File.WriteAllTextAsync(outPath, translatedMarkdown, ct).ConfigureAwait(false);
        return outPath;
    }

    private static (string Frontmatter, string Body) ExtractYamlFrontmatter(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (string.Empty, markdown);
        }

        // Skip optional UTF-8 BOM.
        var start = 0;
        if (markdown[0] == '\uFEFF')
        {
            start = 1;
        }

        // Must start with --- on its own line (allow trailing whitespace, then CRLF/LF).
        if (!StartsWithFence(markdown, start, out var firstFenceEnd))
        {
            return (string.Empty, markdown);
        }

        // Find the closing fence (--- or ...).
        var searchFrom = firstFenceEnd;
        while (searchFrom < markdown.Length)
        {
            var lineEnd = IndexOfLineEnd(markdown, searchFrom, out var lineTerminatorLength);
            var lineLen = lineEnd - searchFrom;
            if (IsFenceLine(markdown, searchFrom, lineLen))
            {
                var fmEnd = lineEnd + lineTerminatorLength;
                var frontmatter = markdown.Substring(0, fmEnd);
                var body = markdown.Substring(fmEnd);
                return (frontmatter, body);
            }
            searchFrom = lineEnd + lineTerminatorLength;
        }

        // No closing fence — treat as not frontmatter.
        return (string.Empty, markdown);
    }

    private static bool StartsWithFence(string s, int start, out int afterLine)
    {
        var lineEnd = IndexOfLineEnd(s, start, out var lineTerminatorLength);
        var lineLen = lineEnd - start;
        if (IsTripleDash(s, start, lineLen))
        {
            afterLine = lineEnd + lineTerminatorLength;
            return true;
        }
        afterLine = start;
        return false;
    }

    private static bool IsFenceLine(string s, int start, int length)
        => IsTripleDash(s, start, length) || IsTripleDot(s, start, length);

    private static bool IsTripleDash(string s, int start, int length)
    {
        if (length < 3)
        {
            return false;
        }
        if (s[start] != '-' || s[start + 1] != '-' || s[start + 2] != '-')
        {
            return false;
        }
        for (int i = start + 3; i < start + length; i++)
        {
            if (s[i] != ' ' && s[i] != '\t')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTripleDot(string s, int start, int length)
    {
        if (length < 3)
        {
            return false;
        }
        if (s[start] != '.' || s[start + 1] != '.' || s[start + 2] != '.')
        {
            return false;
        }
        for (int i = start + 3; i < start + length; i++)
        {
            if (s[i] != ' ' && s[i] != '\t')
            {
                return false;
            }
        }
        return true;
    }

    private static int IndexOfLineEnd(string s, int from, out int terminatorLength)
    {
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == '\r')
            {
                terminatorLength = (i + 1 < s.Length && s[i + 1] == '\n') ? 2 : 1;
                return i;
            }
            if (s[i] == '\n')
            {
                terminatorLength = 1;
                return i;
            }
        }
        terminatorLength = 0;
        return s.Length;
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".md" => "text/markdown",
            ".markdown" => "text/markdown",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }
}
