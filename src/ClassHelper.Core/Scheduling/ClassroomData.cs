namespace ClassHelper.Core.Scheduling;

public sealed record Period(
    Guid Id,
    int Ordinal,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record ScheduleEntry(
    int CycleWeek,
    DayOfWeek DayOfWeek,
    Guid PeriodId,
    string CourseName,
    string? Teacher = null,
    string Color = "#425FC7");

public enum TeachingDayKind
{
    NoClass,
    Normal,
    FollowWeekday
}

public sealed record TeachingDayOverride(
    DateOnly Date,
    TeachingDayKind Kind,
    DayOfWeek? SourceDayOfWeek = null,
    string? Note = null);

public sealed record RosterMember(
    Guid Id,
    string Name,
    string? Number = null,
    bool IsActive = true);

public sealed class ClassroomData
{
    public int CycleLength { get; set; } = 3;

    public DateOnly AnchorMonday { get; set; } = FindMonday(DateOnly.FromDateTime(DateTime.Today));

    public List<Period> Periods { get; init; } = [];

    public List<ScheduleEntry> ScheduleEntries { get; init; } = [];

    public List<TeachingDayOverride> TeachingDayOverrides { get; init; } = [];

    public List<RosterMember> Roster { get; init; } = [];

    public static DateOnly FindMonday(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
