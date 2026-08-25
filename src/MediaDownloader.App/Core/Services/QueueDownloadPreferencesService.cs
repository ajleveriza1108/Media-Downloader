using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MediaDownloader.Core.Services;

public sealed class QueueDownloadPreferences
{
    public string BatchFormat { get; set; } = QueueDownloadPreferencesService.KeepEachItemFormat;
    public int MaxConcurrentDownloads { get; set; } = 5;
}

public sealed class QueueDownloadPreferencesService
{
    public const string KeepEachItemFormat = "Keep each item's format";
    public const string AllAsMp4 = "All as MP4";
    public const string AllAsMp3 = "All as MP3";
    public const int MaximumConcurrentDownloads = 5;

    public static IReadOnlyList<string> BatchFormatChoices { get; } =
    [
        KeepEachItemFormat,
        AllAsMp4,
        AllAsMp3
    ];

    public static IReadOnlyList<int> ConcurrentDownloadChoices { get; } = [1, 2, 3, 4, 5];

    private readonly string _path;

    public QueueDownloadPreferencesService()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaDock",
            "queue-download-preferences.json");
    }

    public QueueDownloadPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Normalize(new QueueDownloadPreferences());
            }

            var json = File.ReadAllText(_path);
            var value = JsonSerializer.Deserialize<QueueDownloadPreferences>(json)
                ?? new QueueDownloadPreferences();
            return Normalize(value);
        }
        catch
        {
            return Normalize(new QueueDownloadPreferences());
        }
    }

    public void Save(QueueDownloadPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = Normalize(preferences);
        preferences.BatchFormat = normalized.BatchFormat;
        preferences.MaxConcurrentDownloads = normalized.MaxConcurrentDownloads;

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(
            preferences,
            new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _path, overwrite: true);
    }

    public static string NormalizeBatchFormat(string? value) =>
        value switch
        {
            AllAsMp4 => AllAsMp4,
            AllAsMp3 => AllAsMp3,
            _ => KeepEachItemFormat
        };

    public static int NormalizeConcurrency(int value) =>
        Math.Clamp(value, 1, MaximumConcurrentDownloads);

    public static QueueDownloadPreferences Normalize(QueueDownloadPreferences preferences) =>
        new()
        {
            BatchFormat = NormalizeBatchFormat(preferences.BatchFormat),
            MaxConcurrentDownloads = NormalizeConcurrency(preferences.MaxConcurrentDownloads)
        };

    public static void RunSelfTestR1637()
    {
        var high = Normalize(new QueueDownloadPreferences
        {
            BatchFormat = AllAsMp3,
            MaxConcurrentDownloads = 99
        });
        if (high.MaxConcurrentDownloads != 5 || high.BatchFormat != AllAsMp3)
        {
            throw new InvalidOperationException(
                "R1.6.37 queue preference contract failed: the five-download ceiling was not enforced.");
        }

        var low = Normalize(new QueueDownloadPreferences
        {
            BatchFormat = "invalid",
            MaxConcurrentDownloads = 0
        });
        if (low.MaxConcurrentDownloads != 1 || low.BatchFormat != KeepEachItemFormat)
        {
            throw new InvalidOperationException(
                "R1.6.37 queue preference contract failed: invalid settings were not normalized safely.");
        }
    }
}
