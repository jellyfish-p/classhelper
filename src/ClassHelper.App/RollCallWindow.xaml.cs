using System.Windows;
using System.Windows.Media.Animation;
using ClassHelper.App.ViewModels;
using ClassHelper.Core.RollCall;

namespace ClassHelper.App;

public partial class RollCallWindow : Window
{
    private readonly ClassroomWorkspace _workspace;
    private RollCallMode _mode;
    private RollCallSession? _session;

    public RollCallWindow(ClassroomWorkspace workspace, RollCallMode mode)
    {
        _workspace = workspace;
        _mode = mode;
        InitializeComponent();
        StartSession();
    }

    private void StartSession()
    {
        ApplyModeStyles();

        try
        {
            _session = new RollCallSession(_workspace.Data.Roster, _mode);
            DrawButton.IsEnabled = true;
            ModeText.Text = _mode == RollCallMode.IndependentRandom ? "独立随机" : "均衡轮选";
            HintText.Text = "准备好后开始抽取";
            ResultNameText.Text = "等待抽取";
            ResultNumberText.Text = string.Empty;
            RefreshRemaining();
        }
        catch (ArgumentException)
        {
            _session = null;
            DrawButton.IsEnabled = false;
            ModeText.Text = "随机点名";
            RemainingText.Text = "名单为空";
            HintText.Text = "请先在主控面板添加固定名单";
            ResultNameText.Text = "暂无名单";
        }
    }

    private void Draw_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var selected = _session.Draw();
        ResultNameText.Text = selected.Name;
        ResultNumberText.Text = string.IsNullOrWhiteSpace(selected.Number) ? string.Empty : $"学号 / 座号 {selected.Number}";
        HintText.Text = "本次抽取结果";
        RefreshRemaining();

        if (SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(280));
            ResultNameText.BeginAnimation(OpacityProperty, fade);
            ResultNumberText.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void Independent_Click(object sender, RoutedEventArgs e)
    {
        _mode = RollCallMode.IndependentRandom;
        StartSession();
    }

    private void Balanced_Click(object sender, RoutedEventArgs e)
    {
        _mode = RollCallMode.BalancedRound;
        StartSession();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _session?.ResetRound();
        HintText.Text = "本轮已重置";
        ResultNameText.Text = "等待抽取";
        ResultNumberText.Text = string.Empty;
        RefreshRemaining();
    }

    private void ApplyModeStyles()
    {
        IndependentModeButton.Style = (Style)FindResource(
            _mode == RollCallMode.IndependentRandom ? "ModeActiveButton" : "ModeInactiveButton");
        BalancedModeButton.Style = (Style)FindResource(
            _mode == RollCallMode.BalancedRound ? "ModeActiveButton" : "ModeInactiveButton");
    }

    private void RefreshRemaining()
    {
        if (_session is null)
        {
            return;
        }

        RemainingText.Text = _mode == RollCallMode.IndependentRandom
            ? $"候选 {_session.CandidateCount} 人"
            : $"本轮剩余 {_session.RemainingInRound} 人";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
