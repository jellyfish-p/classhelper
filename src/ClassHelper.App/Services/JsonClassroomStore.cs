using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassHelper.Core.RollCall;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Services;

public sealed class PreviewData
{
    public List<RosterMember> Roster { get; init; } = [];

    public UpdatePreferences Updates { get; set; } = new();
}

public sealed class UpdatePreferences
{
    public UpdateChannel? Channel { get; set; }

    public bool CheckOnStartup { get; set; } = true;
}

public sealed class JsonClassroomStore
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonClassroomStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassHelper",
            "classroom.preview.json"))
    {
    }

    public JsonClassroomStore(string filePath) => _filePath = Path.GetFullPath(filePath);

    public string FilePath => _filePath;

    public async Task<PreviewData> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            var sample = CreateSampleData();
            await SaveAsync(sample, cancellationToken);
            return sample;
        }

        await using var stream = File.OpenRead(_filePath);
        var data = await JsonSerializer.DeserializeAsync<PreviewData>(stream, _options, cancellationToken);
        if (data is null)
        {
            return CreateSampleData();
        }

        data.Updates ??= new UpdatePreferences();
        return data;
    }

    public async Task SaveAsync(PreviewData data, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("无法确定课堂数据目录。");
            Directory.CreateDirectory(directory);

            var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, data, _options, cancellationToken);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static PreviewData CreateSampleData()
    {
        var data = new PreviewData();
        var names = new[] { "陈晨", "李明", "王雨", "赵一诺", "周子涵", "吴思远", "郑可欣", "孙浩然", "林佳琪", "何俊熙" };
        for (var index = 0; index < names.Length; index++)
        {
            data.Roster.Add(new RosterMember(Guid.NewGuid(), names[index], (index + 1).ToString("00")));
        }

        return data;
    }
}
