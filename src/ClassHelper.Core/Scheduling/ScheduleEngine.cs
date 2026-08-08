namespace ClassHelper.Core.Scheduling;

public sealed record ScheduledPeriod(
    Period Period,
    ScheduleEntry? Entry,
    bool IsCurrent,
    bool IsNext);

public sealed record DaySchedule(
    DateOnly Date,
    int CycleWeek,
    DayOfWeek EffectiveDayOfWeek,
    bool IsNoClass,
    string? CalendarNote,
    IReadOnlyList<ScheduledPeriod> Periods);

public sealed class ScheduleEngine
{
    public int GetCycleWeek(DateOnly date, DateOnly anchorMonday, int cycleLength)
    {
        if (cycleLength is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleLength), "课程周期只能是 1、2 或 3 周。");
        }

        if (anchorMonday.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException("周期起点必须是星期一。", nameof(anchorMonday));
        }

        var weekOffset = FloorDivide(date.DayNumber - anchorMonday.DayNumber, 7);
        return Modulo(weekOffset, cycleLength) + 1;
    }

    public DaySchedule GetDay(ClassroomData data, DateOnly date, TimeOnly now)
    {
        ArgumentNullException.ThrowIfNull(data);

        var calendarOverride = data.TeachingDayOverrides.FirstOrDefault(item => item.Date == date);
        var cycleWeek = GetCycleWeek(date, data.AnchorMonday, data.CycleLength);

        if (calendarOverride?.Kind == TeachingDayKind.NoClass)
        {
            return new DaySchedule(
                date,
                cycleWeek,
                date.DayOfWeek,
                true,
                calendarOverride.Note ?? "今日停课",
                []);
        }

        var effectiveDay = calendarOverride?.Kind == TeachingDayKind.FollowWeekday
            ? calendarOverride.SourceDayOfWeek
                ?? throw new InvalidOperationException("调课日必须指定采用星期几的课表。")
            : date.DayOfWeek;

        var periods = data.Periods
            .OrderBy(period => period.Ordinal)
            .Select(period => new
            {
                Period = period,
                Entry = data.ScheduleEntries.FirstOrDefault(entry =>
                    entry.CycleWeek == cycleWeek
                    && entry.DayOfWeek == effectiveDay
                    && entry.PeriodId == period.Id)
            })
            .ToList();

        var currentIndex = periods.FindIndex(item => now >= item.Period.StartTime && now < item.Period.EndTime);
        var nextIndex = periods.FindIndex(item => item.Period.StartTime > now);

        var scheduledPeriods = periods
            .Select((item, index) => new ScheduledPeriod(
                item.Period,
                item.Entry,
                index == currentIndex,
                index == nextIndex))
            .ToList();

        var note = calendarOverride?.Kind switch
        {
            TeachingDayKind.FollowWeekday => $"采用星期{ToChineseWeekday(effectiveDay)}课表",
            TeachingDayKind.Normal => calendarOverride.Note ?? "正常教学",
            _ => null
        };

        return new DaySchedule(date, cycleWeek, effectiveDay, false, note, scheduledPeriods);
    }

    private static int FloorDivide(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int Modulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;

    private static string ToChineseWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        DayOfWeek.Saturday => "六",
        DayOfWeek.Sunday => "日",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };
}
