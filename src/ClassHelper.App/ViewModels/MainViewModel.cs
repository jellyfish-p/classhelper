using System.Collections.ObjectModel;
using ClassHelper.Core.Scheduling;

namespace ClassHelper.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ClassroomWorkspace _workspace;
    private int _selectedWeek = 1;
    private string _statusMessage = "所有更改保存在本机";

    public MainViewModel(ClassroomWorkspace workspace)
    {
        _workspace = workspace;
        WeekRows = workspace.CreateWeekRows(_selectedWeek);
        Roster = new ObservableCollection<RosterMemberRow>(workspace.Data.Roster.Select(RosterMemberRow.FromModel));
        RefreshToday();
        workspace.Changed += (_, _) => RefreshToday();
    }

    public ObservableCollection<TodayPeriodViewModel> TodayPeriods { get; } = [];

    public ObservableCollection<WeekScheduleRow> WeekRows { get; private set; }

    public ObservableCollection<RosterMemberRow> Roster { get; }

    public string DateLabel { get; private set; } = string.Empty;

    public string CycleLabel { get; private set; } = string.Empty;

    public string CurrentCourseLabel { get; private set; } = string.Empty;

    public string NextCourseLabel { get; private set; } = string.Empty;

    public string RosterSummary => $"固定名单 · {Roster.Count(member => member.IsActive)} 人";

    public string DataFilePath => _workspace.DataFilePath;

    public int SelectedWeek
    {
        get => _selectedWeek;
        set
        {
            if (!SetProperty(ref _selectedWeek, Math.Clamp(value, 1, _workspace.Data.CycleLength)))
            {
                return;
            }

            WeekRows = _workspace.CreateWeekRows(_selectedWeek);
            OnPropertyChanged(nameof(WeekRows));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task SaveWeekAsync()
    {
        await _workspace.SaveWeekAsync(SelectedWeek, WeekRows);
        StatusMessage = $"第 {SelectedWeek} 周课程表已保存";
    }

    public async Task SaveRosterAsync()
    {
        var members = Roster
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new RosterMember(row.Id, row.Name.Trim(), row.Number?.Trim(), row.IsActive))
            .ToList();

        await _workspace.ReplaceRosterAsync(members);
        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = $"固定名单已保存，共 {members.Count} 人";
    }

    public void AddRosterMember()
    {
        Roster.Add(new RosterMemberRow(Guid.NewGuid(), string.Empty, null, true));
        OnPropertyChanged(nameof(RosterSummary));
    }

    public void ImportRosterText(string text)
    {
        var parsed = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(['\t', ',', '，'], 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            .Select(parts => new RosterMemberRow(
                Guid.NewGuid(),
                parts[0],
                parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null,
                true))
            .ToList();

        if (parsed.Count == 0)
        {
            StatusMessage = "没有识别到有效姓名";
            return;
        }

        Roster.Clear();
        foreach (var member in parsed)
        {
            Roster.Add(member);
        }

        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = $"已预览 {parsed.Count} 名成员，请确认后保存";
    }

    public async Task SaveCalendarOverrideAsync(DateOnly date, TeachingDayKind kind, DayOfWeek? sourceDay)
    {
        await _workspace.SetTeachingDayAsync(date, kind, sourceDay);
        StatusMessage = $"{date:yyyy-MM-dd} 的教学安排已保存";
    }

    public void RefreshToday()
    {
        var today = _workspace.GetToday();
        var culture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        DateLabel = $"{today.Date:M 月 d 日} {culture.DateTimeFormat.GetDayName(today.Date.DayOfWeek)}";
        CycleLabel = $"第 {today.CycleWeek} 周" + (today.CalendarNote is null ? string.Empty : $" · {today.CalendarNote}");

        TodayPeriods.Clear();
        foreach (var scheduledPeriod in today.Periods)
        {
            TodayPeriods.Add(TodayPeriodViewModel.FromModel(scheduledPeriod));
        }

        var current = today.Periods.FirstOrDefault(period => period.IsCurrent);
        var next = today.Periods.FirstOrDefault(period => period.IsNext);
        CurrentCourseLabel = today.IsNoClass
            ? today.CalendarNote ?? "今日停课"
            : current?.Entry?.CourseName is { Length: > 0 } currentName
                ? $"正在上课 · {currentName}"
                : "当前为课间或非教学时段";
        NextCourseLabel = next?.Entry?.CourseName is { Length: > 0 } nextName
            ? $"下一节 {next.Period.StartTime:HH:mm} · {nextName}"
            : "今天已无后续课程";

        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(CycleLabel));
        OnPropertyChanged(nameof(CurrentCourseLabel));
        OnPropertyChanged(nameof(NextCourseLabel));
    }
}

public sealed record TodayPeriodViewModel(
    string PeriodName,
    string TimeRange,
    string CourseName,
    string StateLabel,
    string Background,
    string Foreground)
{
    public static TodayPeriodViewModel FromModel(ScheduledPeriod model)
    {
        var courseName = model.Entry?.CourseName ?? "空课";
        if (model.IsCurrent)
        {
            return new TodayPeriodViewModel(
                model.Period.Name,
                $"{model.Period.StartTime:HH:mm}–{model.Period.EndTime:HH:mm}",
                courseName,
                "进行中",
                "#267F73",
                "#FFFFFF");
        }

        if (model.IsNext)
        {
            return new TodayPeriodViewModel(
                model.Period.Name,
                $"{model.Period.StartTime:HH:mm}–{model.Period.EndTime:HH:mm}",
                courseName,
                "下一节",
                "#FFF5E7",
                "#8A5415");
        }

        return new TodayPeriodViewModel(
            model.Period.Name,
            $"{model.Period.StartTime:HH:mm}–{model.Period.EndTime:HH:mm}",
            courseName,
            string.Empty,
            "#F7F7F5",
            "#18212B");
    }
}

public sealed class RosterMemberRow
{
    public RosterMemberRow(Guid id, string name, string? number, bool isActive)
    {
        Id = id;
        Name = name;
        Number = number;
        IsActive = isActive;
    }

    public Guid Id { get; }
    public string Name { get; set; }
    public string? Number { get; set; }
    public bool IsActive { get; set; }

    public static RosterMemberRow FromModel(RosterMember member) =>
        new(member.Id, member.Name, member.Number, member.IsActive);
}
