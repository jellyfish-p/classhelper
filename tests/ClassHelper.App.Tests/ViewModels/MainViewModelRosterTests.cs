using ClassHelper.App.Services;
using ClassHelper.App.ViewModels;
using ClassHelper.Core.RollCall;

namespace ClassHelper.App.Tests.ViewModels;

public sealed class MainViewModelRosterTests
{
    [Fact]
    public void RemoveRosterMember_RemovesRequestedRowOnly()
    {
        var first = new RosterMember(Guid.NewGuid(), "甲", "01");
        var second = new RosterMember(Guid.NewGuid(), "乙", "02");
        var viewModel = CreateViewModel(first, second);

        var removed = viewModel.RemoveRosterMember(viewModel.Roster[0]);

        Assert.True(removed);
        var remaining = Assert.Single(viewModel.Roster);
        Assert.Equal(second.Id, remaining.Id);
    }

    [Fact]
    public void GenerateNumberRange_ReplacesRosterWithUnnamedPaddedNumbers()
    {
        var existing = new RosterMember(Guid.NewGuid(), "原名单", "01");
        var viewModel = CreateViewModel(existing);

        var generated = viewModel.GenerateNumberRange("008", "012");

        Assert.True(generated);
        Assert.Equal(["008", "009", "010", "011", "012"], viewModel.Roster.Select(row => row.Number));
        Assert.All(viewModel.Roster, row => Assert.Equal(string.Empty, row.Name));
        Assert.All(viewModel.Roster, row => Assert.True(row.IsActive));
    }

    [Fact]
    public async Task SaveRosterAsync_PersistsDeletionAndUnnamedNumberRows()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClassHelper.MainViewModelRosterTests.{Guid.NewGuid():N}");
        var store = new JsonClassroomStore(Path.Combine(testDirectory, "classroom.json"));
        var data = new PreviewData();
        data.Roster.Add(new RosterMember(Guid.NewGuid(), "待删除", "99"));
        var viewModel = new MainViewModel(new ClassroomWorkspace(data, store));

        try
        {
            Assert.True(viewModel.RemoveRosterMember(viewModel.Roster[0]));
            Assert.True(viewModel.GenerateNumberRange("01", "03"));
            await viewModel.SaveRosterAsync();

            var saved = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(["01", "02", "03"], saved.Roster.Select(member => member.Number));
            Assert.All(saved.Roster, member => Assert.Equal(string.Empty, member.Name));
            Assert.DoesNotContain(saved.Roster, member => member.Number == "99");
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    private static MainViewModel CreateViewModel(params RosterMember[] roster)
    {
        var data = new PreviewData();
        data.Roster.AddRange(roster);
        return new MainViewModel(new ClassroomWorkspace(data, new JsonClassroomStore()));
    }
}
