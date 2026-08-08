using ClassHelper.App.Services;
using ClassHelper.Core.RollCall;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.ViewModels;

public sealed class ClassroomWorkspace
{
    private readonly JsonClassroomStore _store;

    public ClassroomWorkspace(PreviewData data, JsonClassroomStore store)
    {
        Data = data;
        _store = store;
    }

    public event EventHandler? Changed;

    public PreviewData Data { get; }

    public string DataFilePath => _store.FilePath;

    public async Task ReplaceRosterAsync(IEnumerable<RosterMember> members)
    {
        Data.Roster.Clear();
        Data.Roster.AddRange(members);
        await SaveAndNotifyAsync();
    }

    public async Task SaveUpdatePreferencesAsync(UpdateChannel channel, bool checkOnStartup)
    {
        Data.Updates.Channel = channel;
        Data.Updates.CheckOnStartup = checkOnStartup;
        await SaveAndNotifyAsync();
    }

    private async Task SaveAndNotifyAsync()
    {
        await _store.SaveAsync(Data, CancellationToken.None);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
