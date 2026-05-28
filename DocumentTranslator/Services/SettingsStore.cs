using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DocumentTranslator.Services;

public sealed class Settings
{
	public string Endpoint { get; set; } = string.Empty;

	public string ProtectedKey { get; set; } = string.Empty;

	public string? LastSourceLanguage { get; set; }

	public List<string> LastTargetLanguages { get; set; } = new();
}

public static class SettingsStore
{
	private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("DocumentTranslator.v1");

	private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

	private static string Folder => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"DocumentTranslator");

	private static string FilePath => Path.Combine(Folder, "settings.json");

	public static Settings Load()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				return new Settings();
			}

			var json = File.ReadAllText(FilePath);
			return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
		}
		catch
		{
			return new Settings();
		}
	}

	public static void Save(Settings settings)
	{
		Directory.CreateDirectory(Folder);
		File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, s_jsonOptions));
	}

	public static void SaveCredentials(string endpoint, string apiKey)
	{
		var existing = Load();
		existing.Endpoint = endpoint?.Trim() ?? string.Empty;
		existing.ProtectedKey = ProtectKey(apiKey);
		Save(existing);
	}

	public static void SaveLanguageSelection(string? sourceLanguage, IEnumerable<string> targetLanguages)
	{
		var existing = Load();
		existing.LastSourceLanguage = sourceLanguage;
		existing.LastTargetLanguages = targetLanguages.ToList();
		Save(existing);
	}

	public static string ProtectKey(string? apiKey)
	{
		if (string.IsNullOrEmpty(apiKey))
		{
			return string.Empty;
		}

		var cipher = ProtectedData.Protect(
			Encoding.UTF8.GetBytes(apiKey),
			s_entropy,
			DataProtectionScope.CurrentUser);
		return Convert.ToBase64String(cipher);
	}

	public static string? UnprotectKey(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.ProtectedKey))
		{
			return null;
		}

		try
		{
			var cipher = Convert.FromBase64String(settings.ProtectedKey);
			var plain = ProtectedData.Unprotect(cipher, s_entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(plain);
		}
		catch
		{
			return null;
		}
	}
}
