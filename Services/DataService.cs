using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using CodeDictionary.Models;

namespace CodeDictionary.Services;

public class DataService
{
    private readonly string _dataFilePath;
    private readonly string _stateFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeDictionary"
        );

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _dataFilePath = Path.Combine(appDataPath, "data.json");
        _stateFilePath = Path.Combine(appDataPath, "state.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<CodeDictionaryData> LoadDataAsync()
    {
        if (!File.Exists(_dataFilePath))
        {
            return new CodeDictionaryData();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_dataFilePath);
            return JsonSerializer.Deserialize<CodeDictionaryData>(json, _jsonOptions)
                   ?? new CodeDictionaryData();
        }
        catch
        {
            return new CodeDictionaryData();
        }
    }

    public async Task SaveDataAsync(CodeDictionaryData data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_dataFilePath, json);
    }

    public async Task ExportDataAsync(CodeDictionaryData data, string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".xml")
        {
            await using var stream = File.Create(filePath);
            var serializer = new XmlSerializer(typeof(CodeDictionaryData));
            serializer.Serialize(stream, data);
            return;
        }

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<CodeDictionaryData> ImportDataAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".xml")
        {
            await using var stream = File.OpenRead(filePath);
            var serializer = new XmlSerializer(typeof(CodeDictionaryData));
            return serializer.Deserialize(stream) as CodeDictionaryData ?? new CodeDictionaryData();
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<CodeDictionaryData>(json, _jsonOptions) ?? new CodeDictionaryData();
    }

    public async Task<AppState> LoadStateAsync()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new AppState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath);
            return JsonSerializer.Deserialize<AppState>(json, _jsonOptions)
                   ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public async Task SaveStateAsync(AppState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_stateFilePath, json);
    }
}
