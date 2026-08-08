using System.Collections.ObjectModel;
using ClassHelper.Core.RollCall;

namespace ClassHelper.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ClassroomWorkspace _workspace;
    private string _statusMessage = "所有更改保存在本机";

    public MainViewModel(ClassroomWorkspace workspace)
    {
        _workspace = workspace;
        Roster = new ObservableCollection<RosterMemberRow>(workspace.Data.Roster.Select(RosterMemberRow.FromModel));
        DateLabel = CreateDateLabel();
        workspace.Changed += (_, _) => RefreshOverview();
    }

    public ObservableCollection<RosterMemberRow> Roster { get; }

    public string DateLabel { get; }

    public string RosterSummary => $"固定名单 · {Roster.Count(member => member.IsActive)} 人";

    public string DataFilePath => _workspace.DataFilePath;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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

    public void RefreshOverview()
    {
        OnPropertyChanged(nameof(RosterSummary));
    }

    private static string CreateDateLabel()
    {
        var now = DateTime.Now;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        return $"{now:M 月 d 日} {culture.DateTimeFormat.GetDayName(now.DayOfWeek)}";
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
