using System.IO;
using System.Text.Json;
using ClassHelper.Core.Scheduling;

namespace ClassHelper.App.Services;

public sealed class JsonClassroomStore
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonClassroomStore()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassHelper");
        _filePath = Path.Combine(dataDirectory, "classroom.preview.json");
    }

    public string FilePath => _filePath;

    public async Task<ClassroomData> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            var sample = CreateSampleData();
            await SaveAsync(sample, cancellationToken);
            return sample;
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<ClassroomData>(stream, _options, cancellationToken)
            ?? CreateSampleData();
    }

    public async Task SaveAsync(ClassroomData data, CancellationToken cancellationToken)
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

    private static ClassroomData CreateSampleData()
    {
        var periods = new[]
        {
            new Period(Guid.NewGuid(), 1, "第 1 节", new TimeOnly(8, 0), new TimeOnly(8, 40)),
            new Period(Guid.NewGuid(), 2, "第 2 节", new TimeOnly(8, 50), new TimeOnly(9, 30)),
            new Period(Guid.NewGuid(), 3, "第 3 节", new TimeOnly(10, 0), new TimeOnly(10, 40)),
            new Period(Guid.NewGuid(), 4, "第 4 节", new TimeOnly(10, 50), new TimeOnly(11, 30)),
            new Period(Guid.NewGuid(), 5, "第 5 节", new TimeOnly(14, 0), new TimeOnly(14, 40)),
            new Period(Guid.NewGuid(), 6, "第 6 节", new TimeOnly(14, 50), new TimeOnly(15, 30)),
            new Period(Guid.NewGuid(), 7, "第 7 节", new TimeOnly(15, 50), new TimeOnly(16, 30)),
            new Period(Guid.NewGuid(), 8, "第 8 节", new TimeOnly(16, 40), new TimeOnly(17, 20))
        };

        var courseNames = new[] { "语文", "数学", "英语", "物理", "历史", "生物", "地理", "班会" };
        var colors = new[] { "#425FC7", "#267F73", "#C47A20", "#7867A8", "#A35D59", "#397E9C" };
        var entries = new List<ScheduleEntry>();

        for (var week = 1; week <= 3; week++)
        {
            for (var day = DayOfWeek.Monday; day <= DayOfWeek.Friday; day++)
            {
                for (var index = 0; index < periods.Length; index++)
                {
                    var courseIndex = (week + (int)day + index) % courseNames.Length;
                    entries.Add(new ScheduleEntry(
                        week,
                        day,
                        periods[index].Id,
                        courseNames[courseIndex],
                        null,
                        colors[courseIndex % colors.Length]));
                }
            }
        }

        var data = new ClassroomData
        {
            CycleLength = 3,
            AnchorMonday = ClassroomData.FindMonday(DateOnly.FromDateTime(DateTime.Today))
        };
        data.Periods.AddRange(periods);
        data.ScheduleEntries.AddRange(entries);

        var names = new[] { "陈晨", "李明", "王雨", "赵一诺", "周子涵", "吴思远", "郑可欣", "孙浩然", "林佳琪", "何俊熙" };
        for (var index = 0; index < names.Length; index++)
        {
            data.Roster.Add(new RosterMember(Guid.NewGuid(), names[index], (index + 1).ToString("00")));
        }

        return data;
    }
}
