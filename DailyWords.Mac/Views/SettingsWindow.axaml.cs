using Avalonia.Controls;
using DailyWords.Models;

namespace DailyWords.Mac;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _draftConfig;

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _draftConfig = config.Clone();
        _draftConfig.Normalize();
        ResultConfig = _draftConfig.Clone();

        IntervalComboBox.Text = _draftConfig.RotationIntervalSeconds.ToString();
        AutoPlayCheckBox.IsChecked = _draftConfig.AutoPlay;
        LoopCheckBox.IsChecked = _draftConfig.LoopPlayback;
        ShowPhoneticCheckBox.IsChecked = _draftConfig.ShowPhonetic;
        ShowMeaningCheckBox.IsChecked = _draftConfig.ShowMeaning;
        ShowExampleCheckBox.IsChecked = _draftConfig.ShowExample;
        LockPositionCheckBox.IsChecked = _draftConfig.LockWindowPosition;
        OnlyUnmasteredCheckBox.IsChecked = _draftConfig.ShowOnlyUnmastered;
        OpacitySlider.Value = _draftConfig.WindowOpacity * 100;
    }

    public AppConfig ResultConfig { get; private set; }

    public bool RebuildWordBankRequested { get; private set; }

    public bool ResetPositionRequested { get; private set; }

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveAndClose(rebuildWordBank: false);
    }

    private void SaveAndRebuildButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveAndClose(rebuildWordBank: true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void ResetPositionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ResetPositionRequested = true;
        TipTextBlock.Text = "保存后会把悬浮窗恢复到左下角默认位置。";
        TipTextBlock.IsVisible = true;
        ErrorTextBlock.IsVisible = false;
    }

    private void SaveAndClose(bool rebuildWordBank)
    {
        TipTextBlock.IsVisible = false;
        ErrorTextBlock.IsVisible = false;

        if (!int.TryParse(IntervalComboBox.Text?.Trim(), out var intervalSeconds))
        {
            ShowError("切换间隔请输入数字。");
            return;
        }

        if (intervalSeconds < 3 || intervalSeconds > 3600)
        {
            ShowError("切换间隔请设置在 3 到 3600 秒之间。");
            return;
        }

        _draftConfig.RotationIntervalSeconds = intervalSeconds;
        _draftConfig.AutoPlay = AutoPlayCheckBox.IsChecked == true;
        _draftConfig.LoopPlayback = LoopCheckBox.IsChecked == true;
        _draftConfig.ShowPhonetic = ShowPhoneticCheckBox.IsChecked == true;
        _draftConfig.ShowMeaning = ShowMeaningCheckBox.IsChecked == true;
        _draftConfig.ShowExample = ShowExampleCheckBox.IsChecked == true;
        _draftConfig.LockWindowPosition = LockPositionCheckBox.IsChecked == true;
        _draftConfig.ShowOnlyUnmastered = OnlyUnmasteredCheckBox.IsChecked == true;
        _draftConfig.WindowOpacity = OpacitySlider.Value / 100d;

        if (ResetPositionRequested)
        {
            _draftConfig.WindowLeft = double.NaN;
            _draftConfig.WindowTop = double.NaN;
        }

        _draftConfig.Normalize();
        ResultConfig = _draftConfig.Clone();
        RebuildWordBankRequested = rebuildWordBank;
        Close(true);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }
}
