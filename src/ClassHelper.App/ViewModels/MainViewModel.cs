using System.Collections.ObjectModel;
using System.Globalization;
using ClassHelper.App.Services;
using ClassHelper.Core.RollCall;
using ClassHelper.Core.RosterImport;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int MaximumGeneratedRosterSize = 2000;
    private readonly ClassroomWorkspace _workspace;
    private string _statusMessage = "所有更改保存在本机";
    private UpdateChannel _updateChannel;
    private bool _checkUpdatesOnStartup;

    public MainViewModel(ClassroomWorkspace workspace)
    {
        _workspace = workspace;
        Roster = new ObservableCollection<RosterMemberRow>(workspace.Data.Roster.Select(RosterMemberRow.FromModel));
        DateLabel = CreateDateLabel();
        _updateChannel = workspace.Data.Updates.Channel
            ?? UpdateChannelPolicy.ForInstalledVersion(AppBuildInfo.Version);
        _checkUpdatesOnStartup = workspace.Data.Updates.CheckOnStartup;
        workspace.Changed += (_, _) => RefreshOverview();
    }

    public ObservableCollection<RosterMemberRow> Roster { get; }

    public string DateLabel { get; }

    public string RosterSummary => $"固定名单 · {Roster.Count(member => member.IsActive)} 人";

    public string DataFilePath => _workspace.DataFilePath;

    public string CurrentVersionDisplay => $"v{AppBuildInfo.DisplayVersion}";

    public UpdateChannel UpdateChannel
    {
        get => _updateChannel;
        set => SetProperty(ref _updateChannel, value);
    }

    public bool CheckUpdatesOnStartup
    {
        get => _checkUpdatesOnStartup;
        set => SetProperty(ref _checkUpdatesOnStartup, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task SaveRosterAsync()
    {
        var members = Roster
            .Where(row => !string.IsNullOrWhiteSpace(row.Name) || !string.IsNullOrWhiteSpace(row.Number))
            .Select(row => new RosterMember(
                row.Id,
                row.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(row.Number) ? null : row.Number.Trim(),
                row.IsActive))
            .ToList();

        await _workspace.ReplaceRosterAsync(members);
        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = $"固定名单已保存，共 {members.Count} 人";
    }

    public Task SaveUpdatePreferencesAsync() =>
        _workspace.SaveUpdatePreferencesAsync(UpdateChannel, CheckUpdatesOnStartup);

    public void AddRosterMember()
    {
        Roster.Add(new RosterMemberRow(Guid.NewGuid(), string.Empty, null, true));
        OnPropertyChanged(nameof(RosterSummary));
    }

    public bool RemoveRosterMember(RosterMemberRow? member)
    {
        if (member is null || !Roster.Remove(member))
        {
            return false;
        }

        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = "已从编辑区删除 1 名成员，保存名单后生效";
        return true;
    }

    public bool GenerateNumberRange(string startText, string endText)
    {
        var normalizedStart = startText.Trim();
        var normalizedEnd = endText.Trim();
        if (!int.TryParse(normalizedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(normalizedEnd, NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || start < 0
            || end < start)
        {
            StatusMessage = "请输入有效的起始和结束学号，结束学号不能小于起始学号";
            return false;
        }

        var memberCount = (long)end - start + 1;
        if (memberCount > MaximumGeneratedRosterSize)
        {
            StatusMessage = $"一次最多生成 {MaximumGeneratedRosterSize} 个学号";
            return false;
        }

        var preservePadding = HasLeadingZero(normalizedStart) || HasLeadingZero(normalizedEnd);
        var paddingWidth = preservePadding ? Math.Max(normalizedStart.Length, normalizedEnd.Length) : 0;
        Roster.Clear();
        for (long offset = 0; offset < memberCount; offset++)
        {
            var number = start + offset;
            var numberText = paddingWidth > 0
                ? number.ToString($"D{paddingWidth}", CultureInfo.InvariantCulture)
                : number.ToString(CultureInfo.InvariantCulture);
            Roster.Add(new RosterMemberRow(Guid.NewGuid(), string.Empty, numberText, true));
        }

        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = $"已生成 {memberCount} 个学号，请确认后保存名单";
        return true;
    }

    public void ImportRosterMembers(IEnumerable<ImportedRosterMember> importedMembers, string sourceName)
    {
        var members = importedMembers
            .Select(member => new RosterMemberRow(
                Guid.NewGuid(),
                member.Name,
                member.Number,
                member.IsActive))
            .ToList();

        Roster.Clear();
        foreach (var member in members)
        {
            Roster.Add(member);
        }

        OnPropertyChanged(nameof(RosterSummary));
        StatusMessage = $"已从 {sourceName} 导入预览 {members.Count} 人，请确认后保存";
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

    private static bool HasLeadingZero(string value) => value.Length > 1 && value[0] == '0';
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
