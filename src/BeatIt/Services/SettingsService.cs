using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BeatIt.Models;

namespace BeatIt.Services;

/// <summary>%AppData%\BeatIt\settings.json 을 읽고 쓴다.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,

        // 이동 영역 같은 값이 숫자로 적히면 사람이 파일을 열어봐도 뭔지 모른다. 숫자로 적힌 옛 파일도 그대로 읽힌다.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsService()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BeatIt");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 설정이 깨졌으면 기본값으로 계속 간다. 위젯이 못 뜨는 것보다 낫다.
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        try
        {
            await using FileStream stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions);
        }
        catch (IOException)
        {
            // 저장 실패로 앱을 죽이지 않는다.
        }
    }
}
