using System.Collections.ObjectModel;
using ClassHelper.App.Services;
using ClassHelper.Core.Scheduling;

namespace ClassHelper.App.ViewModels;

public sealed class ClassroomWorkspace
{
    private readonly JsonClassroomStore _store;
    private readonly ScheduleEngine _scheduleEngine;

    public ClassroomWorkspace(ClassroomData data, JsonClassroomStore store, ScheduleEngine scheduleEngine)
    {
        Data = data;
        _store = store;
        _scheduleEngine = scheduleEngine;
    }

    public event EventHandler? Changed;

    public ClassroomData Data { get; }

    public string DataFilePath => _store.FilePath;

    public DaySchedule GetToday() => _scheduleEngine.GetDay(
        Data,
        DateOnly.FromDateTime(DateTime.Now),
        TimeOnly.FromDateTime(DateTime.Now));

    public ObservableCollection<WeekScheduleRow> CreateWeekRows(int cycleWeek) => new(
        Data.Periods
            .OrderBy(period => period.Ordinal)
            .Select(period => new WeekScheduleRow(
                period,
                GetCourseName(cycleWeek, DayOfWeek.Monday, period.Id),
                GetCourseName(cycleWeek, DayOfWeek.Tuesday, period.Id),
                GetCourseName(cycleWeek, DayOfWeek.Wednesday, period.Id),
                GetCourseName(cycleWeek, DayOfWeek.Thursday, period.Id),
                GetCourseName(cycleWeek, DayOfWeek.Friday, period.Id))));

    public async Task SaveWeekAsync(int cycleWeek, IEnumerable<WeekScheduleRow> rows)
    {
        Data.ScheduleEntries.RemoveAll(entry => entry.CycleWeek == cycleWeek);

        foreach (var row in rows)
        {
            AddEntry(cycleWeek, DayOfWeek.Monday, row.PeriodId, row.Monday);
            AddEntry(cycleWeek, DayOfWeek.Tuesday, row.PeriodId, row.Tuesday);
            AddEntry(cycleWeek, DayOfWeek.Wednesday, row.PeriodId, row.Wednesday);
            AddEntry(cycleWeek, DayOfWeek.Thursday, row.PeriodId, row.Thursday);
            AddEntry(cycleWeek, DayOfWeek.Friday, row.PeriodId, row.Friday);
        }

        await SaveAndNotifyAsync();
    }

    public async Task ReplaceRosterAsync(IEnumerable<RosterMember> members)
    {
        Data.Roster.Clear();
        Data.Roster.AddRange(members);
        await SaveAndNotifyAsync();
    }

    public async Task SetTeachingDayAsync(
        DateOnly date,
        TeachingDayKind kind,
        DayOfWeek? sourceDayOfWeek,
        string? note = null)
    {
        Data.TeachingDayOverrides.RemoveAll(item => item.Date == date);
        Data.TeachingDayOverrides.Add(new TeachingDayOverride(date, kind, sourceDayOfWeek, note));
        await SaveAndNotifyAsync();
    }

    public async Task SaveAndNotifyAsync()
    {
        await _store.SaveAsync(Data, CancellationToken.None);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string GetCourseName(int week, DayOfWeek day, Guid periodId) =>
        Data.ScheduleEntries.FirstOrDefault(entry =>
            entry.CycleWeek == week && entry.DayOfWeek == day && entry.PeriodId == periodId)?.CourseName ?? string.Empty;

    private void AddEntry(int week, DayOfWeek day, Guid periodId, string? courseName)
    {
        if (!string.IsNullOrWhiteSpace(courseName))
        {
            Data.ScheduleEntries.Add(new ScheduleEntry(week, day, periodId, courseName.Trim()));
        }
    }
}

public sealed class WeekScheduleRow
{
    public WeekScheduleRow(
        Period period,
        string monday,
        string tuesday,
        string wednesday,
        string thursday,
        string friday)
    {
        PeriodId = period.Id;
        PeriodName = period.Name;
        TimeRange = $"{period.StartTime:HH:mm}–{period.EndTime:HH:mm}";
        Monday = monday;
        Tuesday = tuesday;
        Wednesday = wednesday;
        Thursday = thursday;
        Friday = friday;
    }

    public Guid PeriodId { get; }
    public string PeriodName { get; }
    public string TimeRange { get; }
    public string Monday { get; set; }
    public string Tuesday { get; set; }
    public string Wednesday { get; set; }
    public string Thursday { get; set; }
    public string Friday { get; set; }
}
